# Audio Current Structure Source

This file is the external current-structure source for audio documentation regeneration and stale-token audits. It records the current audio policy shape only. It does not authorize runtime code, prefab, ScriptableObject, profile asset, core audio, enemy audio, UI audio, or BGM changes.

## Gameplay Action Audio

Gameplay action audio is a sparse prefab-local player action SFX lane. It is separate from gameplay core one-shot audio.

`GameplayActionAudioMoment` current public surface:

- `Windup`
- `AssistOutOfRange`
- `NoTarget`
- `Invalid`

Removed / reserved action-audio moments:

- `Execute`
- `Recovery`
- `Contact`
- `ImpactEnemy`
- `Blocked`

Gameplay action timeline still has execute/recovery. Only action-audio moments were removed.

## Push/Flip Planner Policy

Current Push/Flip action-audio planner emits:

- `StartedThisTick` -> `Windup`
- `PlayerActionAttemptSignals` -> `AssistOutOfRange` / `NoTarget` / `Invalid`

Current Push/Flip action-audio planner does not emit:

- `Execute`
- `Recovery`
- `Contact`
- `ImpactEnemy`
- `Blocked`

Push/Flip gameplay action timeline remains active. Push/Flip still starts from `PushPressed` / `FlipPressed`, records `startTick`, `executeTick`, and `recoveryEndTick` in `PlayerActionRuntimeState`, and resolves movement semantics on the gameplay execute tick.

## Player S1 Profile Policy

Player S1 action-audio profile is sparse and prefab-local. It must author current supported moments only.

Current production profile:

- `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset`
- GUID: `42a2e109fc5141ec9e866925a0a85c3b`

Historical note: the profile previously used a `_Test` suffix, but the current production profile name is `Player_S1_GameplayActionAudioProfile.asset`.

Current required Push/Flip coverage:

- Push/Flip `Windup`
- Push/Flip `AssistOutOfRange`
- Push/Flip `NoTarget`
- Push/Flip `Invalid`

Removed raw serialized moment values `1..5` are invalid authoring values and must not be used in profiles.

Reserved removed values:

- `1`: removed `Execute`
- `2`: removed `Contact`
- `3`: removed `ImpactEnemy`
- `4`: removed `Blocked`
- `5`: removed `Recovery`

`AssistOutOfRange = 6`, `NoTarget = 7`, and `Invalid = 8` must not be renumbered.

## Lane Boundaries

Core gameplay one-shot audio owns damage and entity-exit reactions. Push/Flip action audio does not own `ImpactEnemy`, target damage, death, destroy, consume, or blocked reaction feedback by default.

Core one-shot required semantics:

- `PlayerDamage`
- `EnemyDamage`
- `EntityExitItemConsume`
- `EntityExitBoxDestroy`
- `EntityExitEnemyDeath`
- `EntityExitOutOfBounds`

UI audio uses explicit all-cue coverage and hidden `Ui` channel policy. This policy must not be copied into Push/Flip action-audio profiles.

BGM is persistent flow-owned audio and is not comparable to Push/Flip action-audio one-shot moments.

Enemy action/presentation may have windup/execute/recover facts, but enemy presentation phases do not automatically imply required audio cues. Enemy audio uses its own profile/requirement policy. Push/Flip action audio remains a Player-only sparse profile lane.

## Protected Boundaries

- Do not modify runtime code for this source regeneration.
- Do not modify prefabs, ScriptableObjects, audio profile assets, or `.meta` files for this source regeneration.
- Do not add, remove, or renumber `GameplayActionAudioMoment` enum members from this document.
- Do not change the action-audio planner, `AudioBinding`, core audio, enemy audio, UI audio, or BGM from this document.
- Do not imply that gameplay execute/recovery timeline semantics were removed.
