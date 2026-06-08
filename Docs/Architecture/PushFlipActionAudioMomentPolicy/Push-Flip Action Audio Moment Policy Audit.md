# Push/Flip Action Audio Moment Policy Audit

Date: 2026-06-09 KST

## Decision Summary

Push/Flip gameplay is retained. This audit only closes removed action-audio moments.

Current Push/Flip action audio exposes only:

- `Windup`
- `AssistOutOfRange`
- `NoTarget`
- `Invalid`

Removed action-audio moments:

- `Execute`
- `Recovery`
- `Contact`
- `ImpactEnemy`
- `Blocked`

Gameplay action timeline still has execute/recovery. `PlayerActionRuntimeState`, `PlayerControlStateLogic`, `PushPressed`, `FlipPressed`, `MovementExpander`, `PlayerActionSignals`, and Push/Flip execute/recovery presentation facts remain runtime gameplay/presentation contracts.

## Current Runtime Policy

Action lifecycle audio responsibility:

- `StartedThisTick` can emit `Windup`.
- `ExecutedThisTick` emits no Push/Flip action-audio request.
- `ExecutedThisTick && IsRecoveryPhase` emits no Push/Flip action-audio request.

Fake failure feedback responsibility:

- `PlayerActionAttemptSignals.FeedbackKind == AssistOutOfRange` emits `AssistOutOfRange`.
- `PlayerActionAttemptSignals.FeedbackKind == NoTarget` emits `NoTarget`.
- `PlayerActionAttemptSignals.FeedbackKind == Invalid` emits `Invalid`.

No Push/Flip action-audio request exists for `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, or `Blocked`.

## Required Production Coverage

Player S1 required action-audio coverage:

- Push `Windup`
- Push `AssistOutOfRange`
- Push `NoTarget`
- Push `Invalid`
- Flip `Windup`
- Flip `AssistOutOfRange`
- Flip `NoTarget`
- Flip `Invalid`

`Player_S1_GameplayActionAudioProfile.asset` remains the canonical production profile. Its GUID must stay `42a2e109fc5141ec9e866925a0a85c3b`, and `Player_S1.prefab` must continue to reference that GUID.

## Lane Boundaries

Core gameplay one-shot audio owns damage and entity exit reactions such as `PlayerDamage`, `EnemyDamage`, `EntityExitItemConsume`, `EntityExitBoxDestroy`, `EntityExitEnemyDeath`, and `EntityExitOutOfBounds`.

Enemy audio remains enemy-local and archetype/profile governed. Enemy presentation phases do not automatically imply Player Push/Flip action-audio moments.

UI audio keeps its explicit all-cue policy. That policy is not copied into sparse gameplay action audio.

BGM remains persistent flow-owned audio and is outside gameplay action-audio moment policy.

Target reactions, damage, death, destroy, consume, impact, and blocked feedback remain in core gameplay one-shot or other presentation/audio lanes unless a future owner decision explicitly adds a new layered cue.

## Removed Moment Compatibility

Serialized action-audio moment values are reserved:

- `1`: removed `Execute`
- `2`: removed `Contact`
- `3`: removed `ImpactEnemy`
- `4`: removed `Blocked`
- `5`: removed `Recovery`

`AssistOutOfRange = 6`, `NoTarget = 7`, and `Invalid = 8` must not be renumbered.

Profiles must not author rows for reserved moment values. Raw serialized `Moment: 1`, `Moment: 2`, `Moment: 3`, `Moment: 4`, or `Moment: 5` are invalid current authoring.

## Validation Evidence

Required implementation checks:

- no active C# references to `GameplayActionAudioMoment.Execute` or `GameplayActionAudioMoment.Recovery`
- retained action moments still present in enum/planner/tests
- Player S1 profile has no raw removed moment rows
- Push/Flip gameplay runtime symbols remain present
- `Contact`, `ImpactEnemy`, and `Blocked` are not reintroduced

Recommended tests:

- `./run_tests.sh core --filter GameplayActionAudioRuntimeTests`
- `./run_tests.sh core --filter AudioRepositoryAssetSmokeCoreTests`
- `./run_tests.sh core --filter AudioArchitectureTests`
- `./run_tests.sh core`
