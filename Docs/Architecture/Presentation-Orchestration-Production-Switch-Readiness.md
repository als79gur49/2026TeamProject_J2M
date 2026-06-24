# Presentation Orchestration Production Switch Readiness

## Overview

Phase 9M switches Player action animation production ownership to `OrchestrationAnimationExecutor`, Phase 9N hardens its production telemetry, Phase 9O hardens Enemy presentation telemetry, Phase 9P hardens Action audio telemetry, Phase 9Q hardens Enemy audio one-shot telemetry, and Phase 9R closes topology as retained legacy ownership. Invalid/unset Player action animation values still normalize to `LegacyAnimationSync`, and explicit `LegacyAnimationSync` remains the rollback path. Enemy presentation remains `LegacyEnemyPresentationMapper` by default, Action audio remains `LegacyActionAudioController` by default, Enemy audio remains `LegacyEnemyAudioController` by default, and topology remains `LegacyCoordinator` by design, with explicit orchestration modes allowed only for controlled validation. Core gameplay SFX, Damage/death VFX, and Box motion keep their completed production defaults and telemetry/smoke evidence. The readiness matrix continues to define the criteria for moving one presentation domain at a time from legacy execution ownership to explicit orchestration execution ownership.

The current production policy remains:

- Core gameplay SFX default execution mode is `OrchestrationSfxBridgeExecutor`
- Damage/death VFX default execution mode is `OrchestrationExecutor`
- Box motion default execution mode is `OrchestrationMotionExecutor`
- Player action animation default execution mode is `OrchestrationAnimationExecutor`
- every other known presentation orchestration domain default execution mode is still its legacy owner
- non-Core-SFX, non-Damage/death-VFX, non-Box-motion, and non-player-action-animation orchestration execution is explicit configuration only
- duplicate guards remain enabled
- diagnostics are hardened for Core gameplay SFX default owner, request, fallback, deferred, suppression, and duplicate review
- Damage/death VFX now has hardened production telemetry and PlayMode smoke for default owner, legacy skip, semantic damage/death planning and playback, duplicate suppression, same-tick death suppression, missing diagnostics, rollback, non-authoritative behavior, and lifecycle cleanup review
- Box motion now has Phase 9J hardened production telemetry for default owner metadata, legacy source suppression, semantic slide/flip/flip-impact lifecycle, active/pending/completed state, duplicate and missing diagnostics, visualRoot/flip-driver reset, lifecycle cleanup, explicit legacy rollback, non-blocking/input-lock neutrality, and determinism neutrality
- Player action animation now has Phase 9K EditMode readiness evidence, Phase 9L actual Animator PlayMode evidence, Phase 9M production-default evidence, and Phase 9N value-only telemetry evidence for host lifecycle routing, Push/Flip timing, execute lowering, duplicate guard, missing diagnostics, cleanup, action-audio separation, non-blocking behavior, determinism neutrality, and default/governance drift
- Enemy presentation now has Phase 9O value-only telemetry evidence for explicit controlled orchestration mode, semantic Jump/Charge/Death last-cue values, owner attempts, legacy policy skips, duplicate suppression, missing diagnostics, cleanup reason, audio separation, non-blocking behavior, determinism neutrality, and governance drift, while keeping `LegacyEnemyPresentationMapper` as the production default
- Action audio now has Phase 9P value-only telemetry evidence for explicit controlled orchestration mode, semantic action/moment/outcome last-cue values, owner attempts, legacy policy skips, duplicate suppression, missing diagnostics, cleanup reason, non-blocking behavior, determinism neutrality, and governance drift, while keeping `LegacyActionAudioController` as the production default
- Enemy audio now has Phase 9Q value-only telemetry evidence for explicit controlled one-shot orchestration mode, semantic cue/origin/phase last-cue values, owner attempts, legacy policy skips, duplicate suppression, missing diagnostics, cleanup reason, non-blocking behavior, determinism neutrality, and governance drift, while keeping `LegacyEnemyAudioController` and the existing `ChargeActiveLoop` loop owner
- Topology transition now has Phase 9R value-only telemetry evidence for retained legacy production ownership, controlled executor semantic parity, blocking/input-lock mirror behavior, duplicate guard, cleanup, rollback, determinism neutrality, and governance drift; it remains `LegacyCoordinator` by design because the input-lock owner is the coordinator transition controller and not the executor bridge
- PlayMode smoke now covers the actual host/audio lifecycle for Core gameplay SFX default ownership, fallback, topology deferral, suppression, rollback, and non-authoritative behavior
- legacy rollback paths remain available

## Current default policy

All known presentation orchestration domains except Core gameplay SFX, Damage/death VFX, Box motion, and Player action animation must keep legacy as the current default until a dedicated production-switch PR changes exactly one additional domain. Invalid or unset execution mode values must normalize back to the legacy owner, including the four switched domains, so rollback remains safe when stale or corrupt config is encountered.

Production scenes, stage content, and host authoring configuration must not serialize orchestration execution modes as default values except for the Core gameplay SFX, Damage/death VFX, Box motion, and Player action animation production default tokens. Explicit orchestration mode remains allowed in unit tests, targeted fixtures, and controlled integration tests.

## Domain readiness matrix

| Domain | LegacyOwner | OrchestrationOwner | CurrentDefault | ControlledMode | DefaultIsLegacy | InvalidModeNormalizesToLegacy | DuplicateGuardEvidence | DeterminismEvidence | LifecycleCleanupEvidence | BoundaryEvidence | InputLockRisk | AudioUiRisk | RollbackPath | RecommendedStatus |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Topology transition | LegacyCoordinator | ExecutorBridge | LegacyCoordinator | ExecutorBridge | Yes | Yes | `TopologyExecution_ProductionTelemetry_CoversRetainedLegacyOwnerSemanticAndRollbackValues` | `TopologyExecution_LegacyAndExecutorBridgePresenters_PreserveAuthoritativeTickResultOutputs` | `TopologyExecution_ExecutorBridgeMode_CleanupResetsPortAndDiagnostics` | `TopologyAndInputLockExistingPath_RemainsOwnedByCoordinator` | High, input lock observes coordinator presentation phase | Low | Set `TopologyPresentationExecutionMode.LegacyCoordinator` | LegacyRetainedByDesign |
| Damage/death VFX | LegacyExtension | OrchestrationExecutor | OrchestrationExecutor | OrchestrationExecutor | No | Yes | `DamageDeathVfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback` | `DamageDeathVfx_PlayModeSmoke_IsNonAuthoritative` | `DamageDeathVfx_PlayModeSmoke_LifecycleCleanupClearsGuardAndDiagnostics` | `VfxPlanningBoundary_StaysPresentationOnly` | Low | Low | Set `DamageDeathVfxExecutionMode.LegacyExtension` | ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered |
| Box motion | LegacyTrackPlanner | OrchestrationMotionExecutor | OrchestrationMotionExecutor | OrchestrationMotionExecutor | No | Yes | `BoxMotionProductionDefault_PlayMode_DuplicateGuardNormalAndForced` | `BoxMotionProductionDefault_PlayMode_IsNonAuthoritative` | `BoxMotionProductionDefault_PlayMode_LifecycleCleanupClearsTrackAndPose` | `ArchitectureBoundary_AfterBoxMotionReadiness_RemainsSeparated` | Medium, motion can affect perceived input timing | Low | Set `BoxMotionPresentationExecutionMode.LegacyTrackPlanner` | ProductionDefaultOnTelemetryHardened |
| Player action animation | LegacyAnimationSync | OrchestrationAnimationExecutor | OrchestrationAnimationExecutor | OrchestrationAnimationExecutor | No | Yes | `PlayerActionAnimationReadiness_PlayMode_DuplicateGuardNormalAndForced` | `PlayerActionAnimationReadiness_PlayMode_IsNonAuthoritative` | `PlayerActionAnimationReadiness_PlayMode_LifecycleCleanupClearsAnimatorState` | `ArchitectureBoundary_AfterPlayerAnimationSwitch_RemainsSeparated` | Medium, action holds can affect input feel | Low | Set `PlayerActionAnimationExecutionMode.LegacyAnimationSync` | ProductionDefaultOnTelemetryHardened |
| Enemy presentation | LegacyEnemyPresentationMapper | OrchestrationEnemyPresentationExecutor | LegacyEnemyPresentationMapper | OrchestrationEnemyPresentationExecutor | Yes | Yes | `EnemyPresentation_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues` | `EnemyPresentation_OrchestrationRoute_DoesNotMutateAuthoritativeResultOrBlockingState` | `EnemyPresentation_LifecycleCleanup_ClearsGuardDiagnosticsPortAndStaleDriverState` | `EnemyPresentationPlanningBoundary_StaysPresentationOnly` | Medium | Low | Set `EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper` | KeepLegacy |
| Core gameplay SFX | LegacyGameplayAudioController | OrchestrationSfxBridgeExecutor | OrchestrationSfxBridgeExecutor | OrchestrationSfxBridgeExecutor | No | Yes | `CoreSfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback` | `CoreSfx_PlayModeSmoke_IsNonAuthoritative` | `CoreGameplaySfx_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState` | `AudioOwnership_AfterCoreSfxPlayModeSmoke_RemainsSeparated` | Low, non-blocking one-shot | Medium, audio ownership must stay separated | Set `CoreGameplaySfxExecutionMode.LegacyGameplayAudioController` | ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered |
| Action audio | LegacyActionAudioController | OrchestrationActionAudioBridge | LegacyActionAudioController | OrchestrationActionAudioBridge | Yes | Yes | `ActionAudio_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues` | `ActionAudio_OrchestrationRoute_IsNonBlockingAndDoesNotMutateAuthoritativeTickResult` | `ActionAudio_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState` | `ActionAudioPlanningBoundary_StaysActionAudioOwned` | Low | Medium, profile/authoring edge cases remain | Set `ActionAudioExecutionMode.LegacyActionAudioController` | KeepLegacy |
| Enemy audio | LegacyEnemyAudioController | OrchestrationEnemyAudioBridge | LegacyEnemyAudioController | OrchestrationEnemyAudioBridge | Yes | Yes | `EnemyAudio_ProductionTelemetry_CoversOneShotOwnerSemanticLoopAndRollbackValues` | `EnemyAudio_OrchestrationBridgeMode_DoesNotMutateTickResultOrBlockingState` | `EnemyAudio_OrchestrationBridgeMode_LifecycleClearsDiagnosticsPortAndGuard` | `EnemyAudioPlanningBoundary_StaysPlanningOnlyAndDoesNotAbsorbPlaybackOwnership` | Low | Medium, latest audio integration | Set `EnemyAudioExecutionMode.LegacyEnemyAudioController` | KeepLegacy |

## Production switch candidate recommendation

Box motion is `ProductionDefaultOnTelemetryHardened` after Phase 9J. The current production default is `OrchestrationMotionExecutor`, invalid/unset values still normalize to `LegacyTrackPlanner`, and explicit `LegacyTrackPlanner` rollback remains required. Phase 9M switches Player action animation to `OrchestrationAnimationExecutor`, invalid/unset values still normalize to `LegacyAnimationSync`, explicit `LegacyAnimationSync` rollback remains required, and the execute lowering contract remains `AcceptedTemporaryAdapterContract`.

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

Phase 9H switches only Box motion. Phase 9M switches only Player action animation. Phase 9O hardens Enemy presentation telemetry, Phase 9P hardens Action audio telemetry, and Phase 9Q hardens Enemy audio one-shot telemetry, but none of those switch their defaults. Phase 9R closes topology as `LegacyRetainedByDesign`, not pending. Enemy presentation, action audio, enemy audio, and topology transition remain on their legacy production defaults.

## Phase 9K Player action animation readiness assessment

Phase 9K keeps `PlayerActionAnimationExecutionMode.LegacyAnimationSync` as the production/default owner and the invalid/unset normalization fallback. Production scenes, stage content, and host authoring configuration must not serialize `OrchestrationAnimationExecutor`; controlled tests may enable it explicitly with a recording or concrete playback port.

The current player action animation flow remains:

`TickPresentationData player action signals -> PresentationFactFrame animation facts -> AnimationCuePlanner -> PresentationDomain.Animation cue -> non-blocking PresentationPlaybackCue -> GameplayAnimationPresentationExecutor -> IGameplayAnimationPlaybackPort -> GameplayAnimationSyncCoordinator -> PlayerAnimatorDriver`

Current cue vocabulary inventory:

| Cue key | Source fact kind | Source semantic | Action | Phase | Outcome | Target / anchor | Legacy driver command | Orchestration request | Lowering | Animator-facing effect | Action audio counterpart | Blocking |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PlayerPushWindup | Action | PlayerAction | Push | Windup | Started | player entity / entity visual root | Push windup | player action animation request | Windup | `Push_Windup` | `PlayerPushWindup` action-audio cue may exist | No |
| PlayerPushExecute | Action | PlayerAction | Push | Execute | Executed | player entity / entity visual root | Push recovery | player action animation request | Execute -> recovery | `Push_Recovery` | none | No |
| PlayerPushRecovery | Action | PlayerAction | Push | Recovery | Recovery | player entity / entity visual root | Push recovery | player action animation request | Recovery | `Push_Recovery` | none | No |
| PlayerPushBlocked | Action | PlayerAction | Push | Execute | Blocked | player entity / entity visual root | Push recovery | player action animation request | Execute outcome -> recovery | `Push_Recovery` | none; action-attempt audio remains separate | No |
| PlayerPushImpactContact | Action | PlayerAction | Push | Execute | Impact | player entity / entity visual root | Push recovery | player action animation request | Execute outcome -> recovery | `Push_Recovery` | none | No |
| PlayerPushFailed | Action | PlayerActionAttempt | Push | Failed | Failed | player entity / entity visual root | Push windup attempt feedback | player action animation request | Failed -> windup | `Push_Windup` | attempt audio may map to `AssistOutOfRange`, `NoTarget`, or `Invalid` | No |
| PlayerFlipWindup | Action | PlayerAction | Flip | Windup | Started | player entity / entity visual root | Flip windup | player action animation request | Windup | `Flip_Windup` | `PlayerFlipWindup` action-audio cue may exist | No |
| PlayerFlipExecute | Action | PlayerAction | Flip | Execute | Executed | player entity / entity visual root | Flip recovery | player action animation request | Execute -> recovery | `Flip_Recovery` | none | No |
| PlayerFlipRecovery | Action | PlayerAction | Flip | Recovery | Recovery | player entity / entity visual root | Flip recovery | player action animation request | Recovery | `Flip_Recovery` | none | No |
| PlayerFlipBlocked | Action | PlayerAction | Flip | Execute | Blocked | player entity / entity visual root | Flip recovery | player action animation request | Execute outcome -> recovery | `Flip_Recovery` | none; action-attempt audio remains separate | No |
| PlayerFlipImpactContact | Action | PlayerAction | Flip | Execute | Impact | player entity / entity visual root | Flip recovery | player action animation request | Execute outcome -> recovery | `Flip_Recovery` | none | No |
| PlayerFlipFailed | Action | PlayerActionAttempt | Flip | Failed | Failed | player entity / entity visual root | Flip windup attempt feedback | player action animation request | Failed -> windup | `Flip_Windup` | attempt audio may map to `AssistOutOfRange`, `NoTarget`, or `Invalid` | No |

Planner dedupe keys include tick, semantic source, entity, sequence, action plan, action, phase, outcome, and cue key. Executor ownership keys include tick, semantic source, player, cue key, action, phase, sequence, and action plan. Neither key is canonical gameplay state.

Execute cue lowering assessment: `NeedsMorePlayModeEvidence`.

- Current adapter contract is explicit: `PlayerPushExecute -> PlayerPresentationPhase.PushRecovery` and `PlayerFlipExecute -> PlayerPresentationPhase.FlipRecovery`.
- `PlayerAnimatorDriver` has no execute-specific command surface; legacy `ExecutedThisTick` also resolves to the recovery phase.
- EditMode evidence shows Push and Flip are symmetric, concrete driver state parity matches legacy, execute lowering increments `ExecuteCueMappedToLegacyCommandCount`, and execute followed by recovery does not add a second execute signal.
- Production switch is blocked because actual Animator lifecycle PlayMode evidence is still insufficient for execute/recovery timing, transition duplication, and rollback smoke.
- Phase 9K does not add execute-specific Animator parameters, clips, states, or driver APIs. If future PlayMode evidence shows timing or semantic drift, the next status should become `NeedsExecuteDriverSurface` and the driver surface must be designed in a separate PR.

Phase 9K evidence includes:

- `PlayerActionAnimation_DefaultMode_IsOrchestrationExecutor`
- `PlayerActionAnimation_Readiness_ControlledRoutesSupportedCues`
- `PlayerActionAnimation_Readiness_LegacyAndOrchestrationSemanticParity`
- `PlayerActionAnimation_Readiness_ExecuteLoweringContractIsExplicit`
- `PlayerActionAnimation_Readiness_ExecuteAndRecoveryDoNotDuplicateDriverCommand`
- `PlayerActionAnimation_Readiness_ConcreteDriverCommandParity`
- `PlayerActionAnimation_Readiness_DuplicateGuardNormalAndForced`
- `PlayerActionAnimation_Readiness_MissingDiagnosticsSeparated`
- `PlayerActionAnimation_Readiness_LifecycleCleanupClearsState`
- `PlayerActionAnimation_Readiness_ActionAudioVocabularyAndOwnershipRemainSeparated`
- `PlayerActionAnimation_Readiness_IsNonBlockingAndInputLockNeutral`
- `PlayerActionAnimation_Readiness_IsNonAuthoritative`
- `ProductionSwitchReadiness_ReflectsPlayerActionAnimationAssessment`
- `CoreSfxDamageVfxAndBoxMotion_ProductionDefaultsRemainStable`
- `ArchitectureBoundary_AfterPlayerActionAnimationReadiness_RemainsSeparated`

Readiness gate result after Phase 9K: production switch was not recommended. The next PR was Phase 9L Player Action Animation Production PlayMode Smoke Expansion covering actual Animator lifecycle, execute/recovery timing, explicit legacy rollback, and default drift.

## Phase 9L Player action animation PlayMode lifecycle evidence

Phase 9L keeps `PlayerActionAnimationExecutionMode.LegacyAnimationSync` as the production/default owner and the invalid/unset normalization fallback. Production scenes, stage content, and host authoring configuration still must not serialize `OrchestrationAnimationExecutor`; controlled PlayMode tests enable it explicitly.

The Phase 9L fixture uses a synthetic deterministic host path with real runtime boundaries:

`GameplaySceneHost -> GameplayTickViewPresenter -> GameplayTickPresentationCoordinator -> GameplayAnimationPresentationExecutor -> GameplayAnimationSyncPlaybackPort -> GameplayAnimationSyncCoordinator -> PlayerAnimatorDriver -> Animator`

The fixture uses the production player RuntimeAnimatorController at `Assets/3DM/1Player/Player_S1.controller` when available through `AssetDatabase`. It observes value-only snapshots: execution mode, cue/diagnostic counts, driver phase, cross-fade command count, trigger write count, Animator current/next state hashes, and transition flags. It does not store Animator, GameObject, Transform, clip, controller, or state-machine handles in facts, cues, plans, requests, diagnostics, or gameplay state.

Execute cue lowering assessment: `AcceptedTemporaryAdapterContract`.

- Current adapter contract remains explicit: `PlayerPushExecute -> PlayerPresentationPhase.PushRecovery` and `PlayerFlipExecute -> PlayerPresentationPhase.FlipRecovery`.
- Actual Animator lifecycle evidence shows LegacyAnimationSync and explicit OrchestrationAnimationExecutor produce equivalent observable Push/Flip state sequences for windup, execute, recovery, and final cleanup.
- Execute lowering starts the same recovery state path as legacy and a later recovery cue is idempotent at the driver transition level; no extra recovery transition is introduced.
- Push and Flip results are symmetric.
- Blocked, impact-contact, and failed outcomes preserve their semantic cue identity while mapping to the same concrete driver phases as legacy.
- Duplicate guard suppresses same-tick duplicate orchestration playback before a second Animator command is applied.
- Missing animator, driver, binding, and port states remain no-op diagnostic paths without blocking presentation.
- `PresentInitial`, `ResetSession`, and hard cleanup clear executor/guard/port context and driver/Animator-facing transient state according to existing driver reset contracts.
- Action audio remains owned by `GameplayActionAudioPresentationController`; `GameplayActionAudioMoment` vocabulary is unchanged and animation mode does not add or remove action audio requests.
- Animation cues remain non-blocking and input-lock neutral.
- TickResult determinism hash, final entities, event log, objective result, movement result, attack result, and authoritative player action runtime state are unchanged by animation playback.

Phase 9L PlayMode evidence includes:

- `PlayerActionAnimationReadiness_PlayMode_DefaultMode_IsOrchestrationExecutor`
- `PlayerActionAnimationReadiness_PlayMode_ControlledRoutesSupportedCues`
- `PlayerActionAnimationReadiness_PlayMode_ConcreteAnimatorLifecycle`
- `PlayerActionAnimationReadiness_PlayMode_PushWindupExecuteRecoveryTiming`
- `PlayerActionAnimationReadiness_PlayMode_FlipWindupExecuteRecoveryTiming`
- `PlayerActionAnimationReadiness_PlayMode_ExecuteLoweringTransitionGate`
- `PlayerActionAnimationReadiness_PlayMode_BlockedImpactFailedOutcomes`
- `PlayerActionAnimationReadiness_PlayMode_LegacyAndOrchestrationAnimatorParity`
- `PlayerActionAnimationReadiness_PlayMode_ExplicitLegacyRollback`
- `PlayerActionAnimationReadiness_PlayMode_DuplicateGuardNormalAndForced`
- `PlayerActionAnimationReadiness_PlayMode_MissingAnimatorDriverBindingPortAreNoOp`
- `PlayerActionAnimationReadiness_PlayMode_LifecycleCleanupClearsAnimatorState`
- `PlayerActionAnimationReadiness_PlayMode_ActionAudioOwnershipRemainsSeparated`
- `PlayerActionAnimationReadiness_PlayMode_IsNonBlockingAndInputLockNeutral`
- `PlayerActionAnimationReadiness_PlayMode_IsNonAuthoritative`
- `CoreSfxDamageVfxAndBoxMotion_ProductionDefaultsRemainStableAfterPlayerAnimationSmoke`

Readiness gate result after Phase 9L: `CandidateForNextPR`. The next recommended PR is Phase 9M Player Action Animation Production Switch. That PR may change the normal/default owner to `OrchestrationAnimationExecutor`, must keep invalid/unset fallback and explicit rollback as `LegacyAnimationSync`, and must continue to keep topology, enemy presentation, action audio, and enemy audio ownership separated.

## Phase 9M Player action animation production switch

Player action animation production default is now `OrchestrationAnimationExecutor`. The enum zero/default value remains `LegacyAnimationSync`; invalid/unset normalization remains `LegacyAnimationSync`; explicit `LegacyAnimationSync` remains the rollback path.

Phase 9M changes presentation execution ownership only:

- `GameplaySceneHost -> GameplayTickViewPresenter -> GameplayTickPresentationCoordinator` explicitly configures `PlayerActionAnimationExecutionDefaults.ProductionDefault`.
- Legacy player Push/Flip action animation source fields are skipped by owner policy while locomotion, non-action player mapping, death mapping, enemy presentation, topology, action audio, VFX/SFX, and box motion remain owned by their existing paths.
- Default orchestration routes the 12 supported Push/Flip animation cues through `GameplayAnimationPresentationExecutor -> GameplayAnimationSyncPlaybackPort -> GameplayAnimationSyncCoordinator -> PlayerAnimatorDriver -> Animator`.
- Execute cue lowering remains `AcceptedTemporaryAdapterContract`: `PlayerPushExecute -> PlayerPresentationPhase.PushRecovery` and `PlayerFlipExecute -> PlayerPresentationPhase.FlipRecovery`.
- Animation cues remain non-blocking, input-lock neutral, and non-authoritative.
- Action audio remains owned by `GameplayActionAudioPresentationController`; `GameplayActionAudioMoment` vocabulary is unchanged.

Phase 9N hardens Player action animation production telemetry without changing ownership. The value-only telemetry snapshot reports current mode, production default, rollback mode, last cue/action/phase/outcome values, dedupe key, player entity id, owner attempts, legacy policy skips, executor attempts/applies, duplicate suppression, execute-to-recovery lowering, missing target/anchor/binding/driver/animator/port counts, and cleanup reason. The snapshot is built from `PlayerActionAnimationExecutionGuard` and `GameplayAnimationPresentationExecutor` diagnostics only; it does not store `Animator`, `GameObject`, `Transform`, clip, controller, state-machine, `WorldState`, `TickResult`, save/replay state, or determinism data. Existing Phase 9L PlayMode evidence remains the actual Animator lifecycle gate for Push/Flip timing and rollback.

Recommended status after Phase 9N: `ProductionDefaultOnTelemetryHardened`.

Phase 9N Player action animation telemetry evidence includes:

- `PlayerActionAnimation_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues`
- `PlayerActionAnimation_OrchestrationExecutorMode_ForcedDuplicateAttemptBlocksSecondOwner`
- `PlayerActionAnimation_ControlledHostExecutor_DistinguishesMissingDiagnostics`
- `PlayerActionAnimation_OrchestrationRoute_IsNonBlockingCleansLifecycleAndDoesNotMutateTickResult`
- `ProductionSwitchReadiness_ReflectsPlayerActionAnimationSwitch`
- `ArchitectureBoundary_AfterPlayerAnimationSwitch_RemainsSeparated`

Current `CandidateForNextPR` recommendation count: 0. Enemy presentation now has Phase 9O telemetry hardening, Action audio now has Phase 9P telemetry hardening, and Enemy audio now has Phase 9Q one-shot telemetry hardening, but all remain `KeepLegacy`; topology transition is Phase 9R `LegacyRetainedByDesign` because its parity and telemetry evidence pass while its runtime ownership gate does not justify moving the input-lock owner away from the coordinator transition controller.

## Phase 9O Enemy presentation telemetry hardening

Phase 9O keeps `EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper` as the production/default owner and the invalid/unset normalization fallback. Production scenes, stage content, and host authoring configuration must not serialize `OrchestrationEnemyPresentationExecutor`; controlled tests may still enable it explicitly with a recording or concrete playback port.

The value-only `EnemyPresentationProductionTelemetrySnapshot` reports current mode, production default, rollback mode, last cue/kind/phase/outcome values, dedupe key, enemy entity id, owner attempts, legacy policy skips, executor attempts/applies, duplicate suppression, Jump/Charge/Death legacy command mapping counts, missing target/anchor/binding/mapper/driver/animator/port counts, and cleanup reason. The snapshot is built from `EnemyPresentationExecutionGuard` and `GameplayEnemyPresentationExecutor` diagnostics only; it does not store `EnemyAnimatorDriver`, `EnemyViewPresentationMapper`, `Animator`, `GameObject`, `Transform`, clip, controller, `WorldState`, `TickResult`, save/replay state, or determinism data.

Recommended status after Phase 9O: `KeepLegacy`.

Phase 9O Enemy presentation telemetry evidence includes:

- `EnemyPresentation_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues`
- `EnemyPresentation_DuplicateGuard_BlocksForcedSecondExecutorAttemptButNormalModesHaveNoDuplicates`
- `EnemyPresentation_ControlledIntegration_DistinguishesMissingDiagnosticsWithoutExceptions`
- `EnemyPresentation_LifecycleCleanup_ClearsGuardDiagnosticsPortAndStaleDriverState`
- `EnemyPresentation_OrchestrationRoute_DoesNotMutateAuthoritativeResultOrBlockingState`
- `EnemyPresentation_OrchestrationMode_DoesNotChangeEnemyAudioPlanningOrOwnershipVocabulary`
- `EnemyPresentationExecutorBoundary_StaysHostOnlyAndDoesNotLeakRuntimeObjectsToPlans`
- `ProductionSwitchReadiness_ReflectsPlayerActionAnimationSwitch`

Enemy presentation remains legacy after Phase 9O because the production ownership switch still needs separate runtime gate coverage for authored enemy prefabs, rollback smoke, and perceived input/combat timing in actual host scenes.

## Phase 9P Action audio telemetry hardening

Phase 9P keeps `ActionAudioExecutionMode.LegacyActionAudioController` as the production/default owner and the invalid/unset normalization fallback. Production scenes, stage content, and host authoring configuration must not serialize `OrchestrationActionAudioBridge`; controlled tests may still enable it explicitly with a recording or concrete playback port.

The value-only `ActionAudioProductionTelemetrySnapshot` reports current mode, production default, rollback mode, last cue/action/moment/outcome values, dedupe key, owner entity id, owner attempts, legacy policy skips, executor attempts/applies, duplicate suppression, missing owner-view/authoring/profile/binding/unsupported-moment/port counts, optional profile-entry no-op count, and cleanup reason. The snapshot is built from `ActionAudioExecutionGuard` and `GameplayActionAudioPresentationExecutor` diagnostics only; it does not store `AudioSource`, `AudioPlaybackHandle`, `GameplayActionAudioProfile`, `GameplayActionAudioAuthoring`, `GameObject`, `Transform`, `WorldState`, `TickResult`, save/replay state, or determinism data.

Recommended status after Phase 9P: `KeepLegacy`.

Phase 9P Action audio telemetry evidence includes:

- `ActionAudio_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues`
- `ActionAudio_OrchestrationMode_ForcedDuplicateExecutorAttemptBlocksSecondPlayback`
- `ActionAudio_OrchestrationMode_DistinguishesMissingDiagnostics`
- `ActionAudio_OrchestrationMode_AdapterSeparatesOwnerAuthoringProfileBindingAndOptionalEntryNoOps`
- `ActionAudio_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState`
- `ActionAudio_OrchestrationRoute_IsNonBlockingAndDoesNotMutateAuthoritativeTickResult`
- `ActionAudioPlanningBoundary_StaysActionAudioOwned`
- `ProductionSwitchReadiness_ReflectsPlayerActionAnimationSwitch`

Action audio remains legacy after Phase 9P because the production ownership switch still needs separate runtime gate coverage for authored profiles and rollback smoke in actual host scenes.

## Phase 9Q Enemy audio one-shot telemetry hardening

Phase 9Q keeps `EnemyAudioExecutionMode.LegacyEnemyAudioController` as the production/default owner and the invalid/unset normalization fallback. Production scenes, stage content, and host authoring configuration must not serialize `OrchestrationEnemyAudioBridge`; controlled tests may still enable it explicitly with a recording or concrete playback port.

The value-only `EnemyAudioProductionTelemetrySnapshot` reports current mode, production default, rollback mode, last cue/origin/phase values, dedupe key, owner entity id, owner attempts, legacy policy skips, executor attempts/applies, duplicate suppression, missing owner-view/authoring/profile/binding/unsupported-semantic/unsupported-loop/port counts, optional profile-entry no-op count, and cleanup reason. The snapshot is built from `EnemyAudioExecutionGuard` and `GameplayEnemyAudioPresentationExecutor` diagnostics only; it does not store `AudioSource`, `AudioPlaybackHandle`, `EnemyAudioProfile`, `EnemyAudioAuthoring`, `GameObject`, `Transform`, `WorldState`, `TickResult`, save/replay state, or determinism data.

Recommended status after Phase 9Q: `KeepLegacy`.

Phase 9Q Enemy audio telemetry evidence includes:

- `EnemyAudio_ProductionTelemetry_CoversOneShotOwnerSemanticLoopAndRollbackValues`
- `EnemyAudio_OrchestrationBridgeMode_DuplicateGuardBlocksForcedSecondAttempt`
- `EnemyAudio_OrchestrationBridgeMode_ReportsMissingDiagnosticsByCause`
- `EnemyAudio_OrchestrationBridgeMode_IgnoresChargeActiveLoopAndKeepsLegacyLoopOwner`
- `EnemyAudio_OrchestrationBridgeMode_LifecycleClearsDiagnosticsPortAndGuard`
- `EnemyAudio_OrchestrationBridgeMode_DoesNotMutateTickResultOrBlockingState`
- `EnemyAudioPlanningBoundary_StaysPlanningOnlyAndDoesNotAbsorbPlaybackOwnership`
- `ProductionSwitchReadiness_ReflectsPlayerActionAnimationSwitch`

Enemy audio remains legacy after Phase 9Q because one-shot bridge telemetry is not the same as production ownership evidence for authored scene profiles, rollback smoke, and the existing `ChargeActiveLoop` loop owner.

## Phase 9R Topology retained legacy ownership

Phase 9R keeps `TopologyPresentationExecutionMode.LegacyCoordinator` as the production/default owner and the invalid/unset normalization fallback. Production scenes, stage content, and host authoring configuration must not serialize `ExecutorBridge`; controlled tests may still enable it explicitly with a recording or concrete playback port.

The value-only `TopologyProductionTelemetrySnapshot` reports current mode, production default, rollback mode, last transition topology values, rotation kind, source metadata, owner attempts, legacy policy skips, executor attempts/routes, duplicate suppression, invalid/missing-port counts, and the current blocking/input-lock mirror state. The snapshot is built from `TopologyPresentationExecutionGuard`, `TopologyPresentationExecutor` diagnostics, and coordinator blocking state only; it does not store `GameplayTopologyTransitionController`, `GameObject`, `Transform`, `Camera`, `WorldState`, `TickResult`, save/replay state, or determinism data.

Recommended status after Phase 9R: `LegacyRetainedByDesign`.

Phase 9R topology evidence includes:

- `TopologyExecution_ProductionTelemetry_CoversRetainedLegacyOwnerSemanticAndRollbackValues`
- `TopologyExecution_ExecutorBridgeMode_UsesExecutorPortOnceAndSkipsLegacyDirectPath`
- `TopologyExecution_ExecutorBridgeMode_ForcedDoubleExecutorAttemptBlocksSecondOwner`
- `TopologyExecution_ExecutorBridgeMode_BlockingMirrorMatchesControllerState`
- `TopologyExecution_BlockingMirrorResetAndHardCleanup_ClearSnapshot`
- `TopologyExecution_LegacyAndExecutorBridgePresenters_ProduceEquivalentVisualStateAndInputLock`
- `TopologyExecution_LegacyAndExecutorBridgePresenters_PreserveAuthoritativeTickResultOutputs`
- `TopologyAndInputLockExistingPath_RemainsOwnedByCoordinator`
- `TopologyExecutorBoundary_StaysHostOnlyAndDoesNotBecomeDefaultRuntimeOwner`
- `ProductionSwitchReadiness_ReflectsPlayerActionAnimationSwitch`

Topology remains legacy after Phase 9R because the executor bridge can reproduce transition values under controlled tests, but production input-lock ownership still observes the coordinator transition phase and `GameplayTopologyTransitionController` state. The evidence supports explicit controlled mode and rollback, not moving the production owner.

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

Phase 9J did not nominate an immediate next production switch candidate. Phase 9L later promoted Player action animation to `CandidateForNextPR`; Phase 9M completed that switch. Enemy presentation remains legacy until a separate readiness pass evaluates lifecycle, rollback, input feel, and boundary evidence.

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

- Do not switch any additional production default beyond the already completed Core SFX, Damage/death VFX, Box motion, and Player action animation owners.
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
