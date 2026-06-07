# Gameplay Audio Governance

이 문서는 core required gameplay one-shot SFX governance의 active supporting truth-source다.

이 문서는 gameplay host-orchestrated core audio가 semantic drift, controller scope creep, bootstrap/map drift 없이 유지되도록 semantic family rule과 safe expansion protocol을 고정한다.

## 1. Current Boundaries

- gameplay-origin one-shot SFX만 `GameplayTickPresentationCoordinator`와 `GameplayAudioPresentationController`를 통해 orchestration 된다.
- `GameplayAudioSemanticId`는 small and stable core required gameplay one-shot semantic set이다.
- push/flip/action-specific sounds must not be added here by default.
- action-specific gameplay audio는 [Gameplay-Action-Audio-Governance.md](./Gameplay-Action-Audio-Governance.md) 의 profile-local layer로 이동한다.
- semantic/global ownership은 아래를 유지한다.
  - `GameplayAudioMap -> AudioBinding -> AudioDefinition`
- shared runtime ownership은 아래를 유지한다.
  - `IAudioService -> AudioManager -> AudioPlaybackService`
- UI audio는 UI presenter/controller path에 남는다.
- BGM / scene-flow audio는 stage/scene/flow presenter path에 남는다.
- `GameplayAudioPresentationController`는 gameplay one-shot presentation audio만 다루며 generic dispatcher가 아니다.
- persistent BGM ownership/access terminology와 transition governance는 [Bgm-Flow-V1-Guidelines.md](./Bgm-Flow-V1-Guidelines.md) 를 따른다.

## 2. Semantic Family Governance

`GameplayAudioSemanticCatalog`는 governed semantic metadata의 canonical owner다.

현재 governed family는 아래다.

- allowed in v1 host-orchestrated gameplay audio
  - `DamageOneShot`
  - `EntityExitOneShot`
- explicitly out of scope in the current architecture
  - `Locomotion`
  - `JumpLoop`
  - `ActionLoop`
  - `AmbientGameplayBed`
  - `UiInteraction`
  - `BgmFlow`

`RequiredOneShotV1`는 descriptor metadata에서 derive되며 hand-maintained flat list가 아니다.

현재 approved v1 host-orchestrated one-shot semantic set은 아래 exact set으로 고정한다.

- `PlayerDamage`
- `EnemyDamage`
- `EntityExitItemConsume`
- `EntityExitBoxDestroy`
- `EntityExitEnemyDeath`
- `EntityExitOutOfBounds`

semantic growth는 closed-by-default다. 다만 arbitrary하지 않게, governed expansion path를 문서화한다.

## 2.1 Governance Split

- core one-shot semantic governance
  - exact global set
  - required map validation
  - `GameplayAudioSemanticId` / `GameplayAudioMap` / `GameplayAudioRequestPlanner` / `GameplayAudioPresentationController`
- action audio governance
  - profile-local entries
  - duplicate detection
  - category/loop validation
  - optional/required policy per profile or prefab contract
  - action enums are not global required gameplay semantic IDs

core lane의 exact-set governance를 action audio profile completeness rule로 재사용하지 않는다.

## 3. How To Add A New Gameplay Audio Semantic Safely

새 gameplay audio semantic id를 추가할 때는 아래 minimum checklist를 같은 change에서 함께 수행해야 한다.

1. catalog entry를 추가한다.
2. family metadata를 추가한다.
3. required-set review를 수행한다.
4. planner review를 수행한다.
5. tests를 갱신한다.
6. docs/governance note를 갱신한다.

이 비용은 의도적이다.

- semantic drift를 막기 위해서다.
- controller/domain scope creep를 막기 위해서다.
- bootstrap validation과 authored map completeness가 계속 align되게 하기 위해서다.

governed expansion은 허용되지만, family review 없이 “semantic id 하나만 더 추가”하는 ad-hoc growth는 허용되지 않는다.

## 4. Controller Boundary

`GameplayAudioPresentationController`의 책임은 아래로 제한한다.

- precomputed gameplay one-shot request list를 받는다.
- owner view가 살아 있으면 attached playback을 resolve한다.
- owner resolution이 실패하면 `Play2D`로 fallback 한다.
- playback step 이후 pending plan을 즉시 비운다.

금지:

- `PlayBgm` ownership
- UI click / menu / popup audio routing
- locomotion / windup / recovery / loop ownership
- long-lived continuous handle state
- generic cross-domain audio dispatch

future loop or flow audio가 필요해도 이 controller를 넓히지 않는다. 별도 controller/track을 추가해야 한다.
true fade/crossfade execution도 이 controller를 넓히는 방식으로 넣지 않는다.

action-specific windup/contact/blocked/impact SFX는 이 core controller가 아니라 separate gameplay action-audio controller path에 남긴다.
Charge active loop SFX는 이 core controller가 아니라 별도 enemy-local persistent controller path에 남긴다.

## 5. Pending Plan Lifecycle

pending gameplay audio request lifecycle은 아래 exact rule을 따른다.

- `ReplacePendingPlan(...)`
  - non-null planned request list만 받는다.
  - last-write-wins로 기존 pending plan을 교체한다.
  - empty plan replace는 legal이며 pending state를 비운다.
- `PlayPlannedAudio()`
  - current pending plan을 order-preserving하게 한 번만 재생한다.
  - legitimate same-frame repeated request는 dedupe하지 않는다.
  - playback 이후 즉시 pending plan을 clear한다.
- `ClearPendingPlan()`
  - explicit clear이며 idempotent다.
- `ResetSession`, `PresentInitial`, detach/dispose path
  - pending plan을 반드시 clear한다.

normal runtime에서는 replace가 `Present` cycle당 한 번 수행되고, controller는 frame policy를 소유하지 않는다.

## 6. Bootstrap Alignment

- `GameplaySceneHostConfiguration.GameplayPresentationAudioConfig`가 assigned되면 same-root `AudioRuntimeInstaller`가 필요하다.
- `GameplayPresentationAudioConfig`는 typed gameplay host presentation SFX maps를 group하는 data + validation owner다.
- `GameplayPresentationAudioConfig`는 dispatcher, planner, controller factory, service locator, playback owner가 아니다.
- action/enemy prefab-local profiles, UI cue maps, BGM profiles, `StageAudioDefinition`, runtime installers, audio settings bridges는 이 config 밖에 남는다.
- scene-global fallback lookup은 금지한다.
- required semantic validation은 `GameplayAudioSemanticCatalog` source of truth만 사용한다.
- missing required semantic은 host attach/init에서 fail-fast 해야 한다.

이 alignment가 깨지면 semantic catalog, authored map completeness, runtime bootstrap contract가 서로 drift한다.
