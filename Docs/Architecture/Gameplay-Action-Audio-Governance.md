# Gameplay Action Audio Governance

이 문서는 gameplay action-audio profile layer의 active supporting truth-source다.

이 layer는 expanding push/flip/action SFX를 `GameplayAudioSemanticId` 밖으로 분리하기 위한 presentation-side authoring lane이다.

## 1. Governance Split

- core one-shot semantic governance
  - exact global set
  - required map validation
  - `GameplayAudioSemanticId`는 closed core required gameplay one-shot semantic set이다
- action audio governance
  - profile-local entries
  - duplicate detection
  - category/loop validation
  - optional/required policy per profile or prefab contract

`GameplayActionKind`와 `GameplayActionAudioMoment`는 typed authoring axes다. global required gameplay semantic IDs가 아니다.

모든 action profile이 every action/moment combination을 가져야 한다는 global completeness rule은 없다.

## 2. Runtime No-Op vs Production Contract

runtime policy:

- missing owner view => no-op
- missing `GameplayActionAudioAuthoring` => no-op
- missing profile on absent component => no-op

production prefab contract:

- `GameplayActionAudioAuthoring` component가 존재하면 profile must be non-null and valid
- canonical player prefab이 Push/Flip action SFX를 production content policy로 요구받으면 해당 prefab은 authoring + required coverage를 가져야 한다
- optional enemy profiles remain optional unless the prefab declares the component
- primitive fallback views are not auto-mutated with action authoring

canonical player profile identity:

- production asset path: `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset`
- asset name: `Player_S1_GameplayActionAudioProfile`
- GUID: `42a2e109fc5141ec9e866925a0a85c3b`
- the old `_Test` profile name is historical-only and must not be listed as current authored content

v1 canonical-player required coverage:

- `Push`: `Windup`, `AssistOutOfRange`, `NoTarget`, `Invalid`
- `Flip`: `Windup`, `AssistOutOfRange`, `NoTarget`, `Invalid`
- GameplayActionAudioMoment v1 no longer includes `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, or `Blocked`.
- Push/Flip action-audio `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, and `Blocked` cues were removed because they are not emitted by the current production planner.
- gameplay action timeline still has execute/recovery; only the action-audio moments were removed.

## 3. Frozen V1 Moment Mapping

`TickResult`에 audio-specific data를 추가하지 않는다. v1은 actual action lifecycle에는 `PlayerActionSignals`를 읽고, fake Push/Flip attempt failure에는 별도 `PlayerActionAttemptSignals`를 읽는다.

mapping table:

- `Windup` => `StartedThisTick`
- `AssistOutOfRange` => `PlayerActionAttemptSignals.FeedbackKind == AssistOutOfRange`
- `NoTarget` => `PlayerActionAttemptSignals.FeedbackKind == NoTarget`
- `Invalid` => `PlayerActionAttemptSignals.FeedbackKind == Invalid`

rules:

- action kind는 `TickPlayerActionPresentationSignal.ActiveActionKind`가 `Push` 또는 `Flip`일 때만 resolve한다
- fake attempt action kind는 `TickPlayerActionAttemptPresentationSignal.ActionKind`가 `Push` 또는 `Flip`일 때만 resolve한다
- `ActiveActionKind == None` 이면 no action audio를 emit한다
- action lifecycle audio emission은 `Windup` only다
- `ExecutedThisTick` and `IsRecoveryPhase` remain gameplay/presentation timeline facts, but they do not emit action-audio moments
- fake failure moments는 lifecycle moments를 synthesize하지 않고 `AssistOutOfRange`, `NoTarget`, `Invalid`만 emit한다
- same-tick duplicate suppression은 하지 않는다
- multiple authored one-shots on the same tick intentionally layer and all play in order
- `ExplicitPushNotStartable` 같은 pre-start rejection은 fake attempt classification이 `PlayerActionAttemptSignals`를 만든 경우에만 failure audio를 emit한다
- future support가 필요하면 new non-audio presentation facts를 추가하고 audio semantics를 simulation에 넣지 않는다

## 4. Action-Side vs Target-Reaction Layering

action-side sound examples:

- Push windup
- Flip windup
- Push/Flip retained failure feedback

target-reaction sound examples:

- Enemy damage
- Enemy death
- Boss reaction
- entity exit/death
- block and impact presentation feedback owned by their existing presentation lanes

rules:

- core `EnemyDamage`, `EnemyDeath`, and entity exit sounds remain on the existing core one-shot path
- impact and blocked gameplay/presentation signals remain owned by their existing gameplay/presentation lanes; they are not action-audio moments
- action profile does not own enemy damage/death governance
- lethal enemy hit is the explicit v1 exception: when an enemy-local `EnemyAudioCue.Death` is actually planned and playable for the same enemy/entity, core `EnemyDamage` is suppressed for that entity only
- generic `EntityExitEnemyDeath` suppression and lethal `EnemyDamage` suppression are separate policies
- lethal `EnemyDamage` suppression is common enemy-local Death cue policy, not SecBot-specific authoring policy
- future suppression, if needed, must be added explicitly and must not silently replace the core reaction lane

## 5. Shared Diagnostics Boundary

shared `AudioBindingDiagnostics`는 binding-local concerns만 안다.

- null binding
- null definition
- reserved `Master`
- caller-supplied allowed categories
- caller-supplied loop policy
- reserved `AudioBinding.Policy` seam

shared diagnostics는 아래를 알지 않는다.

- `GameplayActionKind`
- `GameplayActionAudioMoment`
- `GameplayAudioSemanticId`
- map completeness
- prefab policy

권위는 shared diagnostics에, feature policy 조합은 caller에 둔다.

## 6. Future Extension Note

v1은 prefab-local `GameplayActionAudioAuthoring`만 사용한다.

later default/stage-wide action audio가 필요하면 `GameplaySceneHostConfiguration`에 field를 하나씩 추가하지 않는다.

prefer a future `GameplayPresentationAudioConfig` that can group:

- core one-shot map
- default action audio profile
- optional enemy/entity profile defaults

## 7. Loop Boundary

action audio v1 is one-shot only.

- `GameplayActionAudioMoment` does not include `Loop`, `SlideLoop`, or `ChargeLoop`
- looping `AudioDefinition` entries are rejected for action profiles
- the action-audio controller stores no playback handles
- future loop/continuous audio requires a separate owner/controller
- Charge active loop audio is handled outside action audio by an enemy-local persistent owner/controller.
