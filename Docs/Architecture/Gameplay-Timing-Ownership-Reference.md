# Gameplay Timing Ownership Reference

## 1. 목적

이 문서는 `Docs/Architecture/Gameplay-Timing-Ownership-Blueprint.md`와
`Docs/Architecture/Gameplay-Timing-Ownership-Implementation-Plan.md`를 실제 코드 수정 단위로 내리기 위한 1단계 참조 문서다.

목표는 세 가지다.

- 현재 timing 필드를 `logic`, `motion`, `animator` 세 범주로 먼저 고정한다.
- 새 타입 이름과 canonical ownership을 코드 수정 전에 고정한다.
- 이후 구현 단계에서 어떤 파일을 어떤 책임으로 수정해야 하는지 터치포인트를 명시한다.

이 문서는 설계의 상위 기준이 아니라 구현용 보조 참조 문서다.
최종 판단 기준은 항상 blueprint와 implementation plan이다.

## 2. 빠른 판정 규칙

새 timing 필드를 추가하거나 기존 필드의 위치를 옮길 때는 먼저 아래 질문으로 분류한다.

### 2-1. logic timing

아래 질문에 `예`면 logic이다.

- 이 값이 gameplay execute 시점을 바꾸는가
- 이 값이 input lock, cooldown, contact threshold, recover tick을 바꾸는가
- 이 값이 replay, hash, phase 결과를 바꾸는가

logic timing은 prefab이나 animator가 소유하면 안 된다.

### 2-2. motion presentation timing

아래 질문에 `예`면 motion이다.

- 이 값이 commit된 시작 pose와 끝 pose 사이의 보간 시간을 바꾸는가
- 이 값이 root motion처럼 보이는 world-space 이동 시간을 바꾸는가
- entity마다 무겁게 또는 가볍게 "보이는" 이동 시간을 바꾸는가

motion timing은 presentation-only다.
logic cadence를 바꾸지 않는다.

### 2-3. animator presentation timing

아래 질문에 `예`면 animator다.

- 이 값이 clip 재생 속도나 hold 시간을 바꾸는가
- 이 값이 crossfade 시간을 바꾸는가
- 이 값이 state 이름, trigger 이름, speed 계산 기준을 바꾸는가

animator timing도 presentation-only다.
logic execute 시점을 바꾸면 안 된다.

## 3. 현재 필드 분류와 목표 canonical owner

### 3-1. Global Shared Timing Layer

아래 값은 계속 `GameplayTimingProfile` 계층에 남는다.

| 현재 필드 | 현재 타입 | 범주 | 목표 canonical owner | 비고 |
| --- | --- | --- | --- | --- |
| `SimulationTicksPerSecond` | `GameplayTimingProfile` | logic | `GameplayTimingProfile` | 전역 tick cadence |
| `InitialMoveDelaySeconds` | `GameplayTimingProfile` | logic | `GameplayTimingProfile` | 입력 시작 지연 |
| `RepeatedMoveIntervalSeconds` | `GameplayTimingProfile` | logic | `GameplayTimingProfile` | 공통 repeated move cadence |
| `BoxSlideStepIntervalSeconds` | `GameplayTimingProfile` | logic | `GameplayTimingProfile` | 박스 authoritative slide cadence |
| `ProjectileStepIntervalSeconds` | `GameplayTimingProfile` | logic | `GameplayTimingProfile` | projectile authoritative cadence |
| `MoveMotionDurationSeconds` | `GameplayTimingProfile` | motion | `GameplayTimingProfile` | 공통 move 보간 기본값 |
| `PushMotionDurationSeconds` | `GameplayTimingProfile` | motion | `GameplayTimingProfile` | 공통 push 보간 기본값 |
| `TopologyMotionDurationSeconds` | `GameplayTimingProfile` | motion | `GameplayTimingProfile` | 공통 topology 전환 기본값 |
| `FlipMotionDurationSeconds` | `GameplayTimingProfile` | motion | `GameplayTimingProfile` | 공통 flip 보간 기본값 |
| `FlipArcHeightInCells` | `GameplayTimingProfile` | motion | `GameplayTimingProfile` | global flip motion 파라미터 |

아래 값은 현재 `GameplayTimingProfile`에 있지만 이후 분리 대상이다.

| 현재 필드 | 현재 타입 | 범주 | 목표 canonical owner | 마이그레이션 방향 |
| --- | --- | --- | --- | --- |
| `PlayerMoveCooldownSeconds` | `GameplayTimingProfile` | logic | `PlayerControlTimingSettings` | 전역 기본값 또는 scene explicit settings로 이동 |
| `PlayerPushContactThresholdSeconds` | `GameplayTimingProfile` | logic | `PlayerControlTimingSettings` | player authoritative timing으로 이동 |

`MaxTicksPerFrame`은 timing ownership이라기보다 runtime loop guard에 가깝다.
이번 refactor의 핵심 ownership 논의에서는 별도 취급하고 현재 위치를 유지한다.

### 3-2. Scene Host Surface

`GameplaySceneHostConfiguration`는 canonical timing 저장소가 아니라 scene bootstrap surface여야 한다.

| 현재 필드 | 범주 | 목표 canonical owner | 1차 정리 방향 |
| --- | --- | --- | --- |
| `InitialMoveDelaySeconds` | logic | `GameplayTimingProfile` | scene explicit global override로 유지 가능 |
| `RepeatedMoveIntervalSeconds` | logic | `GameplayTimingProfile` | scene explicit global override로 유지 가능 |
| `BoxSlideStepIntervalSeconds` | logic | `GameplayTimingProfile` | scene explicit global override로 유지 가능 |
| `ProjectileStepIntervalSeconds` | logic | `GameplayTimingProfile` | scene explicit global override로 유지 가능 |
| `MoveMotionDurationSeconds` | motion | `GameplayTimingProfile` | scene explicit global override로 유지 가능 |
| `PushMotionDurationSeconds` | motion | `GameplayTimingProfile` | scene explicit global override로 유지 가능 |
| `TopologyMotionDurationSeconds` | motion | `GameplayTimingProfile` | scene explicit global override로 유지 가능 |
| `FlipMotionDurationSeconds` | motion | `GameplayTimingProfile` | scene explicit global override로 유지 가능 |
| `FlipArcHeightInCells` | motion | `GameplayTimingProfile` | scene explicit global override로 유지 가능 |
| `PlayerMoveCooldownSeconds` | logic | `PlayerControlTimingSettings` | per-field float 제거 후 settings object 또는 asset 참조로 교체 |
| `PlayerPushContactThresholdSeconds` | logic | `PlayerControlTimingSettings` | per-field float 제거 후 settings object 또는 asset 참조로 교체 |

핵심 규칙:

- host configuration은 "scene이 어떤 settings를 쓸지"를 결정할 수 있다.
- 하지만 player logic timing 의미 자체를 여러 float로 다시 정의하면 안 된다.

### 3-3. Player Authoritative Logic Layer

착수 시점의 `PlayerActionTimingAuthoring`에는 logic과 animator가 섞여 있었다.
아래 네 값은 prefab에서 분리되어야 한다.

| 현재 필드 | 현재 타입 | 범주 | 목표 canonical owner | 목표 이름 |
| --- | --- | --- | --- | --- |
| `pushExecuteDelaySeconds` | `PlayerActionTimingAuthoring` | logic | `PlayerControlTimingSettings` | `PushExecuteDelaySeconds` |
| `pushInputLockDurationSeconds` | `PlayerActionTimingAuthoring` | logic | `PlayerControlTimingSettings` | `PushInputLockDurationSeconds` |
| `flipExecuteDelaySeconds` | `PlayerActionTimingAuthoring` | logic | `PlayerControlTimingSettings` | `FlipExecuteDelaySeconds` |
| `flipInputLockDurationSeconds` | `PlayerActionTimingAuthoring` | logic | `PlayerControlTimingSettings` | `FlipInputLockDurationSeconds` |

`PlayerControlTimingSettings`는 아래 authoritative player 값의 단일 canonical source가 된다.

- `MoveCooldownSeconds`
- `PushContactThresholdSeconds`
- `PushExecuteDelaySeconds`
- `PushInputLockDurationSeconds`
- `FlipExecuteDelaySeconds`
- `FlipInputLockDurationSeconds`

즉, 이후 `GameplayHostRuntimeFactory`는 player prefab에서 authoritative snapshot을 만들면 안 된다.

### 3-4. Actor Motion Presentation Layer

현재 actor-specific motion override 계층은 `UnitLocomotionPresentationAuthoring`와
`EntityMotionPresentationAuthoring`로 분리됐다.

| 값 의미 | 현재 canonical owner | 목표 canonical owner | 비고 |
| --- | --- | --- | --- |
| unit 개별 move motion duration | `UnitLocomotionPresentationAuthoring` | `UnitLocomotionPresentationAuthoring` | 없으면 `EntityMotionPresentationAuthoring`, 그다음 global fallback |
| player / enemy 개별 push motion duration | `EntityMotionPresentationAuthoring` | `EntityMotionPresentationAuthoring` | 없으면 global fallback |
| player / enemy 개별 flip motion duration | `EntityMotionPresentationAuthoring` | `EntityMotionPresentationAuthoring` | 없으면 global fallback |
| box interaction motion duration | `EntityMotionPresentationAuthoring` | `EntityMotionPresentationAuthoring` | `UnitLocomotionPresentationAuthoring`는 box interaction을 소유하지 않음 |
| unit 공통 fallback move motion duration | `EntityMotionPresentationAuthoring` | `EntityMotionPresentationAuthoring` | unit move-only authoring 미설정 시 사용 |

새 타입 이름은 `UnitLocomotionPresentationAuthoring`와 `EntityMotionPresentationAuthoring`으로 고정한다.
둘 다 기본값은 `-1f` sentinel을 사용해 fallback을 표현한다.

### 3-5. Actor Animation Presentation Layer

기존 `PlayerActionTimingAuthoring`의 presentation duration은 3단계에서 `PlayerAnimationTimingAuthoring`로 분리했다.

| 현재 필드 | 현재 타입 | 범주 | 목표 canonical owner | 목표 이름 |
| --- | --- | --- | --- | --- |
| `pushPresentationDurationSeconds` | `PlayerActionTimingAuthoring` | animator | `PlayerAnimationTimingAuthoring` | `pushAnimatorDurationSeconds` |
| `flipPresentationDurationSeconds` | `PlayerActionTimingAuthoring` | animator | `PlayerAnimationTimingAuthoring` | `flipAnimatorDurationSeconds` |

`PlayerAnimatorDriver`에 있는 아래 값은 animator layer에 속하지만, 이번 refactor 1차 범위에서는 driver-local 유지가 가능하다.

| 현재 필드 | 현재 타입 | 범주 | 당장 유지 위치 | 비고 |
| --- | --- | --- | --- | --- |
| `stateTransitionCrossFadeDurationSeconds` | `PlayerAnimatorDriver` | animator | `PlayerAnimatorDriver` | 필요 시 후속 단계에서 animation authoring으로 이동 가능 |
| `idleStateName`, `walkStateName`, `pushStateName`, `flipStateName` | `PlayerAnimatorDriver` | animator | `PlayerAnimatorDriver` | 상태 이름 매핑 |

핵심 규칙:

- `PlayerAnimatorDriver`는 더 이상 logic timing snapshot을 읽지 않는다.
- animator duration override가 없으면 resolved motion duration을 사용한다.

### 3-6. Enemy Logic / Animator 경계

적군 쪽 경계는 아래처럼 유지한다.

| 값 의미 | 목표 canonical owner | 금지 위치 |
| --- | --- | --- |
| `recoverSeconds`/`windupSeconds` authoring, detection range, locomotion cooldown cadence, attack cadence 같은 AI 규칙 | `EnemyAiProfile` | `EnemyAnimatorDriver`, prefab-local motion authoring |
| attack / hit / death trigger, state name, clip tuning | `EnemyAnimatorDriver` 또는 미래 `EnemyAnimationTimingAuthoring` | `EnemyAiProfile` |
| enemy별 animation duration override | `EnemyAnimationTimingAuthoring` | `EnemyAiProfile` |

`EnemyAiProfile`에는 presentation duration 필드를 추가하지 않는다.
적 locomotion cooldown의 runtime counter는 `EntityState.enemyLocomotionCooldownTicks`에 저장된다.

## 4. 목표 타입 이름과 책임 고정

이번 refactor에서 사용할 타입 이름과 책임을 아래처럼 고정한다.

| 타입 | 소유해야 하는 것 | 소유하면 안 되는 것 |
| --- | --- | --- |
| `GameplayTimingProfile` | global shared cadence, global motion defaults | player execute delay, player input lock, enemy animation tuning, actor-specific override |
| `GameplaySceneHostConfiguration` | scene bootstrap input, 어떤 settings를 쓸지에 대한 explicit binding | player logic 의미를 다시 정의하는 per-field duplicated timing |
| `PlayerControlTimingSettings` | player authoritative logic timing 전체 | player animator duration, prefab-local presentation tuning |
| `EnemyAiProfile` | enemy authoritative AI timing / priority / recover / locomotion cadence | animation duration, trigger 이름, clip tuning |
| `UnitLocomotionPresentationAuthoring` | unit move-only presentation override | execute delay, cooldown, box interaction cadence, AI cadence |
| `EntityMotionPresentationAuthoring` | actor-specific fallback move / push / flip / box interaction motion override | execute delay, cooldown, AI cadence |
| `PlayerAnimationTimingAuthoring` | player push / flip animator duration override | execute delay, input lock, contact threshold |
| `PlayerAnimatorDriver` | animation state 적용, optional parameter sync, speed 계산 | authoritative player timing source |
| `EnemyAnimationTimingAuthoring` | enemy animation-only tuning 확장 포인트 | AI decision cadence, locomotion cooldown authority |

## 5. Fallback과 이름 변경 고정

후속 구현 단계에서 아래 이름과 fallback 규칙을 기준으로 작업한다.

### 5-1. 이름 변경

- `pushPresentationDurationSeconds` -> `pushAnimatorDurationSeconds`
- `flipPresentationDurationSeconds` -> `flipAnimatorDurationSeconds`
- 3단계 완료로 `PlayerActionTimingAuthoring`는 `PlayerAnimationTimingAuthoring`로 개명되고 animation-only 역할만 남겼다.
- `ResolveMotionDurationSeconds(TickEntityMotionKind motionKind)`는 장기적으로 `ResolveMotionDurationSeconds(int entityId, TickEntityMotionKind motionKind)`로 바뀐다.

### 5-2. fallback 규칙

motion duration:

```text
unit move-only override
-> entity motion presentation override
-> global shared timing default
```

push / flip / box interaction motion은 아래 순서를 따른다.

```text
entity motion presentation override
-> global shared timing default
```

animator duration:

```text
actor animation override
-> resolved motion duration
-> clip natural length 기반 speed 계산
```

player logic timing:

```text
scene / host configuration explicit settings
-> default PlayerControlTimingSettings asset 또는 hard-coded default
```

## 6. 구현 터치포인트

아래 파일은 후속 단계에서 ownership 정리를 실제 코드로 반영할 핵심 터치포인트다.

| 파일 | 다음 단계 핵심 작업 |
| --- | --- |
| `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayTimingProfile.cs` | player-specific authoritative timing 필드를 global shared timing에서 제거 |
| `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs` | player per-field float를 `PlayerControlTimingSettings` binding으로 치환 |
| `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs` | scene bootstrap이 새 player control settings를 채우도록 수정 |
| `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs` | player prefab authoring에서 authoritative snapshot 생성하는 경로 제거 |
| `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimationTimingAuthoring.cs` | logic 필드 제거 후 animation-only authoring 유지 |
| `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimatorDriver.cs` | animation authoring only read로 정리, motion fallback 사용 |
| `Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/UnitLocomotionPresentationAuthoring.cs` | unit move-only presentation override 경계 유지 |
| `Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EntityMotionPresentationAuthoring.cs` | box interaction / push / flip / fallback move motion ownership 유지 |
| `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs` | entity-aware motion duration resolution 도입 |
| `Assets/_Features/Gameplay/Gameplay_Host/Runtime/DefaultGameplayEntityViewFactory.cs` | 새 authoring 구성요소 연결 시 factory bootstrap 반영 |
| `Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyAiProfile.cs` | enemy locomotion cadence와 animation-only 금지 경계 유지 |
| `Assets/Scenes/CombinedGameplayShowcase.unity` 외 showcase assets | player logic field migration, motion/animation override data migration |

## 7. 1단계 완료 기준

이 문서가 추가된 상태에서 아래 질문에 문서만으로 답할 수 있어야 한다.

- 새 timing 값이 logic, motion, animator 중 어디에 속하는가
- 이 값이 global default인가 actor-specific override인가
- 이 값의 canonical source가 어느 타입인가
- fallback 순서가 무엇인가
- 다음 코드 수정 단계에서 어느 파일을 건드려야 하는가

위 다섯 질문에 답할 수 있으면 `1. 문서 추가` 단계의 목적은 충족된 것으로 본다.
