# Audio Policy Decision Matrix

Date: 2026-06-09 KST

## Core One-Shot Policy

Core gameplay one-shot audio is a small exact required semantic map:

- `PlayerDamage`
- `EnemyDamage`
- `EntityExitItemConsume`
- `EntityExitBoxDestroy`
- `EntityExitEnemyDeath`
- `EntityExitOutOfBounds`

Push/Flip implication: do not add action lifecycle sounds to the core exact-set map by default.

## Action Audio Policy

Action audio is prefab-local and sparse, but the current Push/Flip public moment vocabulary is intentionally minimal.

Current vocabulary:

- `GameplayActionKind`: `Push`, `Flip`
- `GameplayActionAudioMoment`: `Windup`, `AssistOutOfRange`, `NoTarget`, `Invalid`

Current Player S1 required coverage:

- Push/Flip `Windup`
- Push/Flip `AssistOutOfRange`
- Push/Flip `NoTarget`
- Push/Flip `Invalid`

Removed current moments:

- Push/Flip `Execute`
- Push/Flip `Recovery`
- Push/Flip `Contact`
- Push/Flip `ImpactEnemy`
- Push/Flip `Blocked`

Policy:

- `StartedThisTick` can emit `Windup`.
- Push/Flip `ExecutedThisTick` does not emit action-audio.
- Push/Flip recovery phase does not emit action-audio.
- Fake failure feedback emits only `AssistOutOfRange`, `NoTarget`, and `Invalid`.
- Missing owner view, missing `GameplayActionAudioAuthoring`, and missing profile entry no-op at runtime.
- If `GameplayActionAudioAuthoring` exists, its profile must be non-null and valid.
- Non-optional null binding is invalid.
- Optional null binding remains valid for retained moments only; it is not a compatibility window for removed moments.

## UI Audio Policy

UI audio uses explicit all-cue map coverage. Missing cue, duplicate cue, null binding, wrong category, looping definition, attachment slot, and non-null binding policy fail validation.

Push/Flip implication: UI explicit-all-cue policy is not copied into gameplay action audio.

## BGM Policy

BGM is persistent flow-owned audio and remains outside gameplay one-shot/action maps and host SFX arbitration.

Push/Flip implication: BGM policy is only a separation-of-lanes example.

## Enemy Policy

Enemy audio uses runtime no-op plus production requirement metadata. Enemy action state can have windup/execute/recover, but enemy phase vocabulary does not automatically require player Push/Flip action-audio moments.

Push/Flip implication: enemy policy supports separating presentation phases from audio requirements; it does not require `Execute`/`Recovery` to stay authorable in Player action audio.

## Recommended Push/Flip Moment Policy

- Keep `Windup` and fake failure moments production-required and audible.
- Remove `Execute` and `Recovery` from action-audio public surface and planner emission.
- Keep `Contact`, `ImpactEnemy`, and `Blocked` removed from action-audio vocabulary.
- Preserve gameplay action execute/recovery timeline outside action audio.
- Keep damage and target reactions in core/enemy/other presentation lanes by default.

## Rejected Options

- Reject applying core required semantic fail-fast to all action moments.
- Reject applying UI explicit-all-cue map policy to Push/Flip action audio.
- Reject treating removed moments as authoring gaps.
- Reject treating gameplay execute/recovery timeline facts as action-audio moment requirements.
- Reject forcing Player Push/Flip to match Enemy requirement binding unless a future design explicitly adds action-profile requirement metadata.
