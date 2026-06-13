# Enemy Audio/Presentation Policy Comparison

Date: 2026-06-09 KST

This is an investigation artifact for Push/Flip action-audio policy. It does not change enemy runtime or content requirements.

## Enemy Action State Lane

Enemy combat action runtime is compiled from `EnemyAiProfile -> EnemyAiProfileCompiler -> EnemyAiRuntimeDefinition`. `EnemyActionStateLogic` owns combat action state for melee and forward-cell projectile attacks:

- start/windup: active `EnemyActionRuntimeState` with `startTick` and `executeTick`;
- execute: `executionAttempted` at execute tick;
- recover: enemy AI mode and action transition facts after commit.

`EnemyAudioRequestPlanner` maps enemy action presentation signals to audio:

- `StartedThisTick` -> `EnemyAudioCue.Windup`;
- `ExecutedThisTick` -> `EnemyAudioCue.Active`;
- `StartedRecoveryThisTick` -> `EnemyAudioCue.Recover`;
- passive-contact source uses `EnemyAudioCue.PassiveContact` instead of `Active`;
- receiver-cooldown and invincible rejection paths do not emit the normal active cue.

Production requiredness is not global. Enemy audio uses `EnemyAudioRequirementPolicy` and `EnemyAudioRequirementBinding` per production profile. A cue can be Required, Optional, or Disabled for an archetype.

## Enemy Charge Lane

Enemy charge state has presentation phases `Windup`, `Active`, and `Recover`. The view mapper exposes `StartedChargeWindupThisTick`, `StartedChargeActiveThisTick`, and `StartedChargeRecoverThisTick`.

Audio is narrower than presentation:

- one-shot planner can emit `EnemyAudioCue.Active` when charge active starts;
- `EnemyChargeLoopAudioPresentationController` owns persistent `EnemyAudioCue.ChargeActiveLoop` during active phase;
- RocketFace production policy requires `ChargeActiveLoop` and disables one-shot `Active`;
- charge windup/recover do not currently have required one-shot charge cues.

Policy implication: presentation phases do not automatically imply required audio moments.

## Enemy Jump Lane

Enemy jump presentation has windup and airborne states. `EnemyViewPresentationMapper` tests lock that jump windup/airborne are separate from attack windup/recover.

Audio does not mirror all jump presentation phases:

- `EnemyAudioRequestPlanner` emits `EnemyAudioCue.Landing` only when `LandedThisTick`;
- no current enemy audio cue exists for jump windup or airborne;
- JumpChaserAstra/Astreton production profile requires `Move`, `Landing`, and `Death`.

Policy implication: a timeline state can be visual-only and still be a deliberate audio no-op.

## Enemy Passive Contact Lane

Passive contact is not ordinary enemy `Active` audio:

- `EnemyAudioRequestPlanner` maps passive-contact execution to `EnemyAudioCue.PassiveContact`;
- tests assert passive contact does not fall back to `Active`;
- Startis production profile requires `PassiveContact`;
- non-passive-contact profiles disable or omit it.

Passive contact damages the player, so core gameplay one-shot `PlayerDamage` remains the player reaction lane. Enemy-local `PassiveContact` is an enemy-source cue, not a replacement for core damage reaction.

## Enemy Presentation Signals

`EnemyViewPresentationMapper` and enemy audio planners are siblings. The mapper produces `EnemyViewPresentationState` for animation/presentation drivers, while audio planners read `TickResult.PresentationData` directly.

That means:

- enemy presentation state is not the source of audio requests;
- animation windup/execute/recover does not automatically create audio coverage;
- audio policy is profile/archetype-owned through enemy audio assets and requirement binding.

## Enemy Audio Hooks If Any

Enemy audio hooks in current HEAD:

- `EnemyAudioRequestPlanner` for one-shots: `Move`, `Death`, `Windup`, `Landing`, `Active`, `Recover`, `ForwardCellImpact`, `StationaryActive`, `PassiveContact`.
- `EnemyAudioPresentationController` for resolving `EnemyAudioAuthoring -> EnemyAudioProfile`.
- `EnemyChargeLoopAudioPresentationController` for persistent `ChargeActiveLoop`.
- `GameplayTickPresentationCoordinator` plays enemy one-shot audio after core and action audio.

Runtime no-op cases:

- missing owner view;
- missing `EnemyAudioAuthoring`;
- missing profile;
- missing cue entry;
- inactive/stale charge loop signal.

Production validation cases:

- required cue missing fails;
- optional cue missing passes;
- disabled cue carrying a binding fails;
- authored `ChargeActiveLoop` must be looping and attached.

## Comparison With Player Push/Flip Action Audio

| Aspect | Player Push/Flip | Enemy combat action | Enemy charge | Enemy jump | Enemy passive contact |
|---|---|---|---|---|---|
| Runtime state owner | Player action presentation signals from tick result | `EnemyActionStateLogic` and AI mode/action state | Charge capability/runtime presentation | Jump capability/runtime presentation | Passive contact capability/action source |
| Timeline vocabulary | Gameplay timeline keeps windup/execute/recovery; action audio exposes `Windup` and fake failure moments only | Windup, execute/active, recover | Windup, active, recover | Windup, airborne, landing | Contact execution |
| Presentation signal | `TickPlayerActionPresentationSignal`, `TickPlayerActionAttemptPresentationSignal` | `TickEnemyActionPresentationSignal` | `TickEnemyChargePresentationSignal` | `TickEnemyJumpPresentationSignal` | `TickEnemyActionPresentationSignal` with passive-contact source |
| Audio request source | `GameplayActionAudioRequestPlanner` | `EnemyAudioRequestPlanner` | one-shot planner plus charge loop controller | `EnemyAudioRequestPlanner` landing only | `EnemyAudioRequestPlanner` passive-contact cue |
| Profile/map authoring | `GameplayActionAudioProfile` on player prefab | `EnemyAudioProfile` plus requirement binding | `EnemyAudioProfile` plus loop-specific validation | `EnemyAudioProfile` plus requirement binding | `EnemyAudioProfile` plus requirement binding |
| Missing audio policy | Missing owner/authoring/entry no-op; production Player S1 required subset | Runtime no-op; production Required/Disabled metadata | Runtime no-op/stop; production `ChargeActiveLoop` can be required | Runtime no-op for non-landing phases | Runtime no-op; Startis requires `PassiveContact` |
| Tests | `GameplayActionAudioRuntimeTests`, audio smoke/governance docs | `EnemyAudioRuntimeTests`, requirement policy/binding tests | charge loop and requirement binding tests | mapper tests plus audio landing coverage | passive-contact audio runtime tests |

## Policy Implications

Answers to required enemy questions:

- Enemy action state has windup/execute/recover and enemy audio can map those to `Windup`/`Active`/`Recover`, but coverage is archetype-specific. There is no single global enemy action-audio profile equivalent to Player Push/Flip.
- Enemy jump/charge presentation has richer phase state than audio. Jump windup/airborne are visual/presentation only today; charge active loop is audio-owned, while charge windup/recover one-shots are not required.
- Passive contact is not handled only by core audio: enemy-local `PassiveContact` can play when authored, and core `PlayerDamage` remains the damage reaction.
- Push/Flip should stay Player-only action audio. Enemy can inform the idea of sparse runtime plus production requirement metadata, but it does not require Push/Flip to add enemy-style requirement bindings now.
- Current docs/tests do not show a plan to generalize Player Push/Flip action-audio into a unified player/enemy action-audio lane.

Recommended conclusion: align terminology and documentation, not runtime ownership. Player Push/Flip action audio should remain its own sparse profile lane unless a future PR explicitly introduces production requirement metadata for action profiles.
