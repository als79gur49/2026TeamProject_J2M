# Push/Flip Action Audio Execute/Recovery Removal PR Plan

Date: 2026-06-09 KST

This plan is retained as a historical planning artifact. The `Execute`/`Recovery` removal decision has already been applied.
No profile explicit-null/add-entry migration is planned for `Execute`/`Recovery`.
Future reintroduction would require a new public-surface decision.

Gameplay action timeline still has execute/recovery.
Only action-audio moments were removed.

## Scope

Remove:

- `GameplayActionAudioMoment.Execute`
- `GameplayActionAudioMoment.Recovery`
- Push/Flip action-audio planner emissions from `ExecutedThisTick`
- Push/Flip action-audio planner emissions from `ExecutedThisTick && IsRecoveryPhase`
- docs/tests that described `Execute` and `Recovery` as current action-audio seams before the removal decision

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

## Applied Implementation State

- Removed raw serialized values `1..5` remain invalid authoring values.
- Current surface is `Windup`, `AssistOutOfRange`, `NoTarget`, and `Invalid`.
- Removed surface is `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, and `Blocked`.
- The planner emits lifecycle action audio for `Windup` only.
- Fake failure request planning emits `AssistOutOfRange`, `NoTarget`, and `Invalid`.
- `Player_S1_GameplayActionAudioProfile.asset` and GUID `42a2e109fc5141ec9e866925a0a85c3b` remain unchanged.

## Follow-Up Candidate: Removed Moment Governance Hardening

- Keep raw serialized values `1..5` invalid.
- Ensure Player S1 profile contains only current supported moments.
- Keep docs/tests preventing `Contact`, `ImpactEnemy`, `Blocked`, `Execute`, and `Recovery` reintroduction.

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
