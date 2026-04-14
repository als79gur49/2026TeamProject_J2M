> Archived historical document.
> This file is not part of the active truth-source chain. Start with [Docs/Architecture/README.md](../../Architecture/README.md).
> Archive index: [Docs/Archive/README.md](../README.md).

# Gameplay Timing Ownership Blueprint

## 1. 목적

이 문서는 현재 `Assets/_Features/Gameplay`의 결정론적 Tick 시뮬레이션 구조 위에서 gameplay timing과 presentation timing의 소유권을 어떻게 분리할지에 대한 최종 기준을 정의한다.

이 문서는 아래 문서의 상위 구조 원칙을 따른다.

- `Docs/Architecture/Hybrid-Architecture-Rulebook.md`
- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
- `Docs/Architecture/Cube-Surface-Gameplay-Blueprint.md`
- `Docs/Architecture/Cube-Surface-3D-Presentation-Blueprint.md`
- `Docs/Architecture/Enemy-AI-FSM-Blueprint.md`
- `Docs/Architecture/Player-Action-Windup-Presentation-Blueprint.md`

핵심 목표는 다음 네 가지다.

- gameplay authority와 presentation tuning의 책임을 분리한다.
- 전역 기본값, 액터별 로직 값, 액터별 연출 값을 서로 다른 계층에 둔다.
- 플레이어와 적군이 같은 확장 규칙을 따르도록 구조를 정규화한다.
- 향후 적별 이동 시간, 적별 애니메이션, 신규 액션이 추가되어도 기존 계층 규칙을 유지한다.

## 2. 적용 대상

현재 코드베이스에서 이 문서와 직접 연결되는 핵심 지점은 다음과 같다.

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayTimingProfile.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimationTimingAuthoring.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimatorDriver.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyAnimatorDriver.cs`
- `Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/UnitLocomotionPresentationAuthoring.cs`
- `Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EntityMotionPresentationAuthoring.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAiConfig.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs`
- `Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlStateLogic.cs`

정리하면:

- `GameplayTimingProfile`는 현재 공통 cadence와 일부 player logic timing을 함께 가진다.
- 착수 시점의 `PlayerActionTimingAuthoring`는 player authoritative logic timing과 player animation timing을 함께 가졌다.
- `EnemyAiProfile`는 적군 로직 전용 profile이며 view timing을 가지지 않는다.
- `EnemyAnimatorDriver`는 적군 presentation만 담당하고 별도 duration authoring을 가지지 않는다.

## 3. 현재 구조의 문제

현재 구조에는 아래 세 가지 문제가 있다.

### 3-1. player logic timing과 player view timing이 prefab authoring에 섞여 있다

착수 시점의 `PlayerActionTimingAuthoring`는 다음을 동시에 가졌다.

- `pushExecuteDelaySeconds`
- `pushInputLockDurationSeconds`
- `flipExecuteDelaySeconds`
- `flipInputLockDurationSeconds`
- `pushPresentationDurationSeconds`
- `flipPresentationDurationSeconds`

앞 네 값은 authoritative logic이고, 뒤 두 값은 animator presentation이다.

즉, 현재 player prefab이 gameplay authority 일부를 소유하고 있다.

### 3-2. push / flip presentation duration이 전역값과 player 전용값으로 이중화되어 있다

현재 `PushMotionDurationSeconds`와 `FlipMotionDurationSeconds`는 root motion 보간 시간으로 쓰이고, `pushPresentationDurationSeconds`와 `flipPresentationDurationSeconds`는 player animator와 visual hold 시간으로 쓰인다.

이 구조는 의도된 override가 아니라, 기본적으로 서로 드리프트할 수 있는 이중 관리 구조다.

### 3-3. 미래의 적군 확장에 대응할 공용 presentation override 계층이 없다

현재는 적군이 전역 motion duration만 공유한다. 이는 현재 요구사항에는 충분하지만, 향후 다음 요구사항이 오면 구조가 막힌다.

- 적 A는 무겁게 0.5초 동안 이동
- 적 B는 가볍게 0.15초 동안 이동
- 적 C는 이동은 0.25초지만 공격 모션 hold는 0.6초
- 적 D는 같은 AI 규칙을 쓰되 다른 애니메이션 세트 사용

지금 구조에서 이를 추가하면 player 쪽의 예외 구조를 적군에도 복제할 위험이 있다.

## 4. 핵심 철학

- gameplay authority는 host / config / authoritative state가 소유한다.
- prefab과 animator driver는 presentation만 소유한다.
- 전역 timing은 "모두가 반드시 공유해야 하는 기본값"만 가진다.
- 액터별 gameplay 차이는 actor logic profile에 둔다.
- 액터별 motion presentation 차이는 actor presentation profile에 둔다.
- 액터별 animator tuning 차이는 actor animation authoring에 둔다.
- 같은 의미의 시간은 한 계층에만 canonical source를 둔다.
- override는 허용하되, fallback 순서가 문서와 코드에서 일치해야 한다.

한 줄 요약:

> 로직 차이는 logic profile에서, 연출 차이는 presentation profile에서, 공통 기본값은 global timing에서 관리한다.

## 5. 최종 계층 구조

최종 구조는 아래 네 계층으로 분리한다.

### 5-1. Global Shared Timing Layer

공통 tick cadence와 공통 motion 기본값만 가진다.

대표 타입:

- `GameplayTimingProfile`

소유해야 하는 값:

- `SimulationTicksPerSecond`
- `InitialMoveDelaySeconds`
- `RepeatedMoveIntervalSeconds`
- `BoxSlideStepIntervalSeconds`
- `ProjectileStepIntervalSeconds`
- `MoveMotionDurationSeconds`
- `PushMotionDurationSeconds`
- `FlipMotionDurationSeconds`
- `TopologyMotionDurationSeconds`
- `FlipArcHeightInCells`

소유하면 안 되는 값:

- player execute delay
- player input lock
- player contact threshold
- enemy recover tick
- enemy attack cadence
- actor-specific animation duration override

### 5-2. Actor Authoritative Logic Timing Layer

액터별 gameplay authority를 가진다.

대표 타입:

- 신규 `PlayerControlTimingSettings`
- 기존 `EnemyAiProfile`

#### PlayerControlTimingSettings의 책임

- `MoveCooldownSeconds`
- `PushContactThresholdSeconds`
- `PushExecuteDelaySeconds`
- `PushInputLockDurationSeconds`
- `FlipExecuteDelaySeconds`
- `FlipInputLockDurationSeconds`

#### EnemyAiProfile의 책임

- 감지 범위
- 공격 범위
- 이동 / 공격 priority
- recover tick
- locomotion cooldown cadence와 그에 대응하는 runtime state update
- 향후 enemy gameplay cadence가 실제로 달라질 경우 해당 authoritative 값

중요:

- "적이 실제로 덜 자주 움직여야 한다"는 요구는 `EnemyAiProfile` 계층이다.
- "적이 같은 gameplay cadence지만 더 무겁게 보여야 한다"는 요구는 presentation 계층이다.

### 5-3. Actor Motion Presentation Layer

엔티티 root motion 보간 시간을 actor별로 override하는 계층이다.

대표 타입:

- `UnitLocomotionPresentationAuthoring`
- 신규 `EntityMotionPresentationAuthoring`
- 또는 동일 의미의 `EntityMotionPresentationProfile`

소유해야 하는 값:

- `moveMotionDurationSeconds` for unit move-only prefab tuning
- `pushMotionDurationSeconds`
- `flipMotionDurationSeconds`
- 필요 시 `topologyMotionDurationSeconds` 또는 actor-specific motion curve 정보

규칙:

- `UnitLocomotionPresentationAuthoring`는 unit prefab의 move presentation override만 소유한다.
- `EntityMotionPresentationAuthoring`는 box interaction과 공용 push / flip / fallback move override를 소유한다.
- 값이 `-1` 또는 unset이면 문서화된 fallback 순서를 사용한다.
- 이 계층은 gameplay authority를 가지지 않는다.
- player와 enemy가 모두 같은 규칙으로 사용한다.

### 5-4. Actor Animation Presentation Layer

Animator clip 재생 속도, crossfade, state hold, state name mapping 같은 순수 animation tuning을 담당한다.

대표 타입:

- 신규 `PlayerAnimationTimingAuthoring`
- 필요 시 신규 `EnemyAnimationTimingAuthoring`
- `PlayerAnimatorDriver`
- `EnemyAnimatorDriver`

소유해야 하는 값:

- `pushAnimatorDurationSeconds`
- `flipAnimatorDurationSeconds`
- `stateTransitionCrossFadeDurationSeconds`
- animator state / trigger name

규칙:

- animation duration은 기본적으로 resolved motion duration을 따른다.
- actor-specific clip tuning이 필요할 때만 explicit override를 둔다.
- animation authoring은 gameplay logic 시간을 소유하지 않는다.

## 6. 최종 소유권 표

| 값 종류 | 최종 소유 계층 | 대표 타입 | 비고 |
| --- | --- | --- | --- |
| simulation tick rate | Global Shared Timing | `GameplayTimingProfile` | 모든 시스템 공통 |
| repeated move cadence | Global Shared Timing | `GameplayTimingProfile` | 기본 player cadence fallback로 사용 가능 |
| box slide cadence | Global Shared Timing | `GameplayTimingProfile` | box interaction authoritative cadence |
| player move cooldown | Player Authoritative Logic | `PlayerControlTimingSettings` | prefab 금지 |
| player push contact threshold | Player Authoritative Logic | `PlayerControlTimingSettings` | prefab 금지 |
| player push / flip execute delay | Player Authoritative Logic | `PlayerControlTimingSettings` | prefab 금지 |
| enemy recover tick | Enemy Authoritative Logic | `EnemyAiProfile` | AI 전이 규칙 |
| enemy locomotion cooldown cadence | Enemy Authoritative Logic | `EnemyAiProfile` | runtime state는 `EntityState.enemyLocomotionCooldownTicks` |
| global move / push / flip motion duration | Global Shared Timing | `GameplayTimingProfile` | 공통 기본값 |
| unit move-only motion duration | Actor Motion Presentation | `UnitLocomotionPresentationAuthoring` | unit prefab move override, entity motion/global로 fallback |
| actor-specific push / flip / box interaction motion duration | Actor Motion Presentation | `EntityMotionPresentationAuthoring` | player / enemy / box 공용 |
| player push / flip animator duration | Actor Animation Presentation | `PlayerAnimationTimingAuthoring` | 필요 시만 override |
| enemy attack / recover / jump animation tuning | Actor Animation Presentation | `EnemyAnimationTimingAuthoring` | animation-only, AI cadence 금지 |

## 7. Fallback 규칙

모든 timing resolution은 아래 순서를 따른다.

### 7-1. unit move motion fallback

```text
unit move-only override
-> entity motion override
-> global shared timing default
```

### 7-2. non-move motion fallback

```text
entity motion override
-> global shared timing default
```

### 7-3. animator duration fallback

```text
actor animation override
-> resolved motion duration
-> clip natural length 기반 speed 계산
```

### 7-4. player logic timing fallback

```text
scene / host configuration explicit value
-> project default player control timing asset 또는 hard-coded default
```

중요:

- player logic timing은 prefab에서 fallback하지 않는다.
- enemy logic timing은 animator에서 fallback하지 않는다.
- unit move-only authoring은 box interaction cadence나 push / flip authority를 소유하지 않는다.

## 8. 금지 규칙

아래 구조는 금지한다.

- player prefab이 authoritative execute delay를 소유하는 구조
- enemy animator tuning이 `EnemyAiProfile`에 섞이는 구조
- prefab-local unit move authoring이 enemy locomotion cooldown이나 box interaction cadence를 바꾸는 구조
- actor-specific motion duration을 `GameplayTimingProfile`에 개별 필드로 계속 추가하는 구조
- `HeavyEnemyMoveDurationSeconds`, `ScoutEnemyMoveDurationSeconds` 같은 전역 특수 필드를 host config에 누적하는 구조
- animator 현재 state를 읽고 gameplay 판정을 바꾸는 구조
- clip 이름을 기준으로 로직 cooldown이나 wind-up을 결정하는 구조
- view layer의 duration이 authoritative state transition을 직접 지배하는 구조

금지 예시:

```csharp
public float HeavyEnemyMoveDurationSeconds;
public float ScoutEnemyMoveDurationSeconds;
```

```csharp
if (animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
{
    canDealDamage = true;
}
```

## 9. 명명 규칙

이 문서 이후 timing 관련 이름은 아래 규칙을 따른다.

- `*MotionDuration*`: root motion 또는 transform 보간 시간
- `*AnimatorDuration*`: animator clip 체감 재생 시간
- `*ExecuteDelay*`: action 시작 후 실제 intent execute까지의 논리 시간
- `*InputLockDuration*`: 입력이 잠기는 authoritative 시간
- `*Cooldown*`: action 또는 move 종료 후 재행동 제한 시간
- `*Threshold*`: 특정 상태에 진입하기 위한 누적 조건 시간
- `*RecoverTicks*`: FSM recover 단계의 authoritative tick 수

같은 의미를 가진 값을 다른 이름으로 중복 도입하지 않는다.

## 10. 확장 규칙

### 10-1. 새 player action 추가 시

예: `Dash`

- gameplay authority가 필요하면 `PlayerControlTimingSettings`에 `DashExecuteDelaySeconds`, `DashInputLockDurationSeconds`를 추가한다.
- root motion override가 필요하면 unit move-only인지 여부를 먼저 판정하고 `UnitLocomotionPresentationAuthoring` 또는 `EntityMotionPresentationAuthoring` 해석 경로에 `Dash` motion kind를 추가한다.
- animator tuning이 필요하면 `PlayerAnimationTimingAuthoring`에 `dashAnimatorDurationSeconds`를 추가한다.

### 10-2. 새 enemy archetype 추가 시

- AI 규칙 차이는 `EnemyAiProfile` 또는 그 하위 설정에서 확장한다.
- 보기만 다르면 `UnitLocomotionPresentationAuthoring`, `EntityMotionPresentationAuthoring`, `EnemyAnimationTimingAuthoring` 중 맞는 presentation surface로 처리한다.
- gameplay cadence 차이와 animation cadence 차이를 같은 필드에 넣지 않는다.

### 10-3. 적별 이동 시간이 달라질 때

질문은 먼저 아래 둘 중 무엇인지 판정한다.

- gameplay가 다른가
- presentation만 다른가

판정 결과:

- gameplay가 다르면 `EnemyAiProfile`
- presentation만 다르면 move-only는 `UnitLocomotionPresentationAuthoring`, 그 외 motion은 `EntityMotionPresentationAuthoring`

### 10-4. 적별 공격 연출 시간이 달라질 때

- 판정 시점, 피해 시점, recover tick이 다르면 `EnemyAiProfile`
- 애니메이션 hold, crossfade, clip speed만 다르면 `EnemyAnimationTimingAuthoring`

## 11. 미래의 적군 애니메이션 확장에 대한 기준

현재 `EnemyAnimatorDriver`는 parameter / trigger mapping만 가진다. 이는 좋은 출발점이다.

향후 적군 애니메이션이 복잡해져도 아래 규칙을 유지한다.

- `EnemyAnimatorDriver`는 view-only adapter다.
- enemy animation authoring은 optional이어야 한다.
- enemy archetype별 animation tuning은 AI profile이 아니라 animation authoring 또는 presentation profile이 소유한다.
- 같은 enemy AI profile을 다른 애니메이션 세트와 조합 가능해야 한다.

즉, 최종 조합은 아래와 같다.

```text
EnemyAiProfile
  + UnitLocomotionPresentationAuthoring
  + EntityMotionPresentationAuthoring
  + EnemyAnimationTimingAuthoring
```

AI와 animation을 분리해 조합 가능하게 유지한다.

## 12. 현재 코드 기준의 정리 방향

현재 구조에서 가장 먼저 정리해야 하는 대상은 player 쪽이다.

- `GameplayTimingProfile`에서 player authoritative logic timing 제거
- `PlayerAnimationTimingAuthoring`에 logic timing을 다시 넣지 않음
- player authoritative timing은 host / config 쪽으로 이동
- player presentation duration은 animation-only 의미로 재명명
- actor motion override는 player / enemy 공용 계층으로 추가

적군 쪽은 다음 원칙을 유지하며 확장한다.

- `EnemyAiProfile`는 logic-only 유지
- 적별 motion duration은 공용 presentation override로 확장
- 적별 animation tuning은 별도 animation authoring으로 확장

## 13. 최종 원칙

이 문서 이후 timing 확장은 항상 아래 질문 순서로 판단한다.

1. 이 값이 authoritative gameplay 결과를 바꾸는가
2. 이 값이 root motion 보간만 바꾸는가
3. 이 값이 animator 체감만 바꾸는가
4. 이 값이 전역 기본값인가, actor별 override인가

판정 결과에 따라 계층을 선택한다.

- gameplay authority를 바꾸면 logic profile
- root motion만 바꾸면 motion presentation
- animator 체감만 바꾸면 animation authoring
- 모두의 기본값이면 global timing

이 규칙을 어기는 확장은 금지한다.
