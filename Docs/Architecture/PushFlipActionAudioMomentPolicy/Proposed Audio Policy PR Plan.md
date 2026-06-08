# Push/Flip Action Audio Execute/Recovery Removal PR Plan

Date: 2026-06-09 KST

This plan removes Push/Flip action-audio `Execute` and `Recovery` moments. It does not remove Push/Flip gameplay.

## Scope

Remove:

- `GameplayActionAudioMoment.Execute`
- `GameplayActionAudioMoment.Recovery`
- Push/Flip action-audio planner emissions from `ExecutedThisTick`
- Push/Flip action-audio planner emissions from `ExecutedThisTick && IsRecoveryPhase`
- docs/tests that describe `Execute` and `Recovery` as current optional action-audio seams

Preserve:

- Push/Flip gameplay action execute/recovery timeline
- `PlayerActionRuntimeState`
- `PlayerControlStateLogic`
- `PushPressed` / `FlipPressed`
- `MovementExpander` Push/Flip branches
- `TickPresentationData.PlayerActionSignals`
- `PlayerActionAttemptSignals`
- `Windup`, `AssistOutOfRange`, `NoTarget`, and `Invalid` action-audio moments
- core gameplay one-shot audio, enemy audio, UI audio, and BGM

## Implementation

- Reserve old enum numeric values `1-5`; keep `AssistOutOfRange = 6`, `NoTarget = 7`, and `Invalid = 8`.
- Remove removed moments from `GameplayActionAudioMomentCatalog`.
- Update `GameplayActionAudioRequestPlanner` so lifecycle action audio emits `Windup` only.
- Keep fake failure request planning unchanged.
- Reject profile rows carrying raw removed moment values.
- Keep `Player_S1_GameplayActionAudioProfile.asset` and GUID `42a2e109fc5141ec9e866925a0a85c3b` unchanged unless a stale row is found.

## Tests

Required:

- `git diff --check`
- `./run_tests.sh core --filter GameplayActionAudioRuntimeTests`
- `./run_tests.sh core --filter AudioRepositoryAssetSmokeCoreTests`
- `./run_tests.sh core --filter AudioArchitectureTests`
- `./run_tests.sh core`

Optional gameplay safety lanes if risk warrants:

- `./run_tests.sh core --filter PlayerMovementInputTests`
- `./run_tests.sh core --filter MovementPhaseScenarioTests`

UI lane is not required unless UI audio code or UI audio tests are touched.

## Acceptance Searches

- `rg -n "GameplayActionAudioMoment\\.Execute|GameplayActionAudioMoment\\.Recovery" Assets Docs`
- `rg -n "Execute|Recovery|Contact|ImpactEnemy|Blocked" Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles --glob "*.asset"`
- `rg -n "GameplayActionAudioMoment\\.Windup|GameplayActionAudioMoment\\.AssistOutOfRange|GameplayActionAudioMoment\\.NoTarget|GameplayActionAudioMoment\\.Invalid" Assets Docs`
- `rg -n "Player_S1_GameplayActionAudioProfile|42a2e109fc5141ec9e866925a0a85c3b" Assets Docs`
- `rg -n "PlayerActionRuntimeState|PlayerActionKind\\.Push|PlayerActionKind\\.Flip|PushPressed|FlipPressed|MovementExpander" Assets Docs`

Docs may mention removed moment names only as removed/historical policy, not as current optional seams.

## Risk / Rollback

Risk:

- enum serialized value stability
- stale docs/tests preserving old optional-seam policy
- accidental confusion between action-audio `Execute`/`Recovery` and gameplay action execute/recovery timeline

Rollback:

- restore enum members and planner emissions
- restore tests/docs
- rerun targeted action-audio/core tests
