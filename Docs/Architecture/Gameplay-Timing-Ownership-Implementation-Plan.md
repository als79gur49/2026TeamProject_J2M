# Gameplay Timing Ownership Implementation Plan

## 1. 목적

이 문서는 `Docs/Architecture/Gameplay-Timing-Ownership-Blueprint.md`를 실제 Unity C# 구현 작업으로 내리기 위한 실행 계획서다.

목표는 timing 관련 필드를 단순히 옮기는 것이 아니라, 향후 유지보수와 확장 시 아래 규칙이 깨지지 않도록 코드 구조를 고정하는 것이다.

- gameplay authority와 presentation tuning을 분리한다.
- player와 enemy가 같은 timing ownership 규칙을 따른다.
- 전역 기본값과 actor별 override를 명시적으로 구분한다.
- 향후 적별 이동 시간, 신규 액션, 적별 애니메이션 세트가 추가되어도 계층 규칙을 유지한다.

이 계획서는 다음 문서를 따른다.

- `Docs/Architecture/Gameplay-Timing-Ownership-Blueprint.md`
- `Docs/Architecture/Gameplay-Timing-Ownership-Reference.md`
- `Docs/Architecture/Hybrid-Architecture-Rulebook.md`
- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
- `Docs/Architecture/Enemy-AI-FSM-Blueprint.md`
- `Docs/Architecture/Player-Action-Windup-Presentation-Blueprint.md`

## 2. 현재 상태

2026-04-04 기준 현재 코드베이스는 다음 상태다.

- `GameplayTimingProfile`가 공통 cadence와 player 일부 authoritative timing을 함께 가진다.
- `GameplaySceneHostConfiguration`가 global timing과 player logic timing 일부를 함께 가진다.
- `PlayerAnimationTimingAuthoring`가 player animator presentation timing만 가진다.
- `PlayerAnimatorDriver`는 `PlayerAnimationTimingAuthoring`를 읽어 animator duration을 해석한다.
- `EnemyAiProfile`는 적군 authoritative logic 전용 구조로 이미 잘 분리되어 있다.
- `EnemyAnimatorDriver`는 적군 presentation-only 구조로 비교적 얇다.
- `GameplayTickPresentationCoordinator`는 entity별이 아니라 motion kind별 전역 duration만 해석한다.

즉:

- player authoritative timing과 animator timing 분리는 3단계까지 완료됐고
- enemy는 상대적으로 분리되어 있으며
- 공용 actor presentation override 계층은 아직 없다

## 3. 구현 원칙

- authoritative timing은 prefab에서 읽지 않는다.
- prefab authoring은 presentation-only 정보만 소유한다.
- 공통 기본값은 `GameplayTimingProfile`에만 둔다.
- actor-specific gameplay 차이는 actor logic profile에 둔다.
- actor-specific view 차이는 actor presentation profile에 둔다.
- 같은 의미의 시간을 둘 이상의 canonical source가 소유하면 안 된다.
- override는 허용하되, resolution order가 테스트 가능해야 한다.
- migration 중간 단계에서도 replay / hash / 테스트 안정성을 깨지 않는다.

## 4. 최종 산출물

이번 리팩터링의 최종 산출물은 다음과 같다.

- `GameplayTimingProfile` 정리
- 신규 `PlayerControlTimingSettings`
- 신규 `EntityMotionPresentationAuthoring`
- 신규 또는 개명된 `PlayerAnimationTimingAuthoring`
- 필요 시 future hook로 `EnemyAnimationAuthoring`
- `GameplaySceneHostConfiguration`의 책임 재배치
- `GameplayHostRuntimeFactory`의 player timing bootstrap 정리
- `GameplayTickPresentationCoordinator`의 entity-aware motion duration resolution
- migration guard 테스트
- 문서화된 확장 규칙

## 5. 비범위

아래 기능은 이번 작업의 1차 범위에 포함하지 않는다.

- enemy Blackboard 구조 변경
- enemy pathfinding 고도화
- animator graph 자체 리디자인
- VFX / SFX 시스템 개편
- `Dash`, `ChargeAttack`, `Cast` 등 신규 액션 추가
- enemy animation event 기반 타이밍 시스템 도입

## 6. 단계별 작업 계획

### 6-1. 1단계: 용어와 ownership 표면 고정

목표는 기존 필드의 의미를 코드와 문서에서 동일하게 해석하도록 용어를 먼저 고정하는 것이다.

작업:

- `Gameplay-Timing-Ownership-Blueprint.md`와 본 계획서를 기준 문서로 채택
- 신규 `Gameplay-Timing-Ownership-Reference.md`를 1단계 구현 참조 문서로 추가
- timing 필드를 `logic`, `motion`, `animator` 세 범주로 분류
- 새 타입 이름과 책임을 문서 기준과 구현 터치포인트 기준으로 고정

완료 조건:

- 새 필드 추가 요청이 들어와도 어느 계층에 넣을지 문서 기준으로 판정 가능하다.

### 6-2. 2단계: player authoritative timing을 prefab에서 분리

목표는 player gameplay authority가 prefab에 의존하지 않도록 만드는 것이다.

신규 후보 파일:

- `Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlTimingSettings.cs`

작업:

- `PlayerControlTimingSettings` 추가
- `MoveCooldownSeconds`
- `PushContactThresholdSeconds`
- `PushExecuteDelaySeconds`
- `PushInputLockDurationSeconds`
- `FlipExecuteDelaySeconds`
- `FlipInputLockDurationSeconds`
- validation과 snapshot 생성 로직 추가

수정 대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs`

완료 조건:

- player authoritative timing이 host / config에서만 결정된다.
- `GameplayHostRuntimeFactory`가 더 이상 player prefab에서 authoritative logic timing을 읽지 않는다.

진행 상태:

- 2026-04-04 구현 완료
- `PlayerControlTimingSettings`와 `PlayerControlTimingAuthoritativeSnapshot`를 도입했다.
- `GameplaySceneHostConfiguration`는 `PlayerControlTiming`을 authoritative source로 해석한다.
- `GameplayShowcaseSceneInstallerBase`는 새 settings를 채우고, 기존 `playerMoveCooldownSeconds`는 migration fallback으로만 유지한다.
- `GameplayHostRuntimeFactory`는 더 이상 player prefab의 `CreateAuthoritativeSnapshot` 경로를 사용하지 않는다.
- `GameplayTimingProfile`의 player 전용 필드는 아직 남아 있지만, 현재는 `PlayerControlTiming`에서 유도된 compatibility bridge로만 사용된다.
- `PlayerAnimationTimingAuthoring` 정리는 3단계에서 완료했다.

### 6-3. 3단계: player animation authoring 분리 및 재명명

목표는 player animation tuning을 authoritative logic과 분리하는 것이다.

신규 또는 변경 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimationTimingAuthoring.cs`

적용 방향:

- 기존 `PlayerActionTimingAuthoring`를 `PlayerAnimationTimingAuthoring`로 개명
- prefab logic fields 제거
- animation-only authoring으로 축소

남길 값:

- `pushAnimatorDurationSeconds`
- `flipAnimatorDurationSeconds`

선택적으로 유지:

- `stateTransitionCrossFadeDurationSeconds`

수정 대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimatorDriver.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/DefaultGameplayEntityViewFactory.cs`
- 관련 prefab / test utility

완료 조건:

- player prefab에는 presentation-only timing만 남는다.
- `PlayerAnimatorDriver`는 animation authoring만 읽는다.

진행 상태:

- 2026-04-04 구현 완료
- `PlayerActionTimingAuthoring`를 `PlayerAnimationTimingAuthoring`로 개명했다.
- player prefab timing authoring에는 `pushAnimatorDurationSeconds`, `flipAnimatorDurationSeconds`만 남겼다.
- `PlayerAnimatorDriver`, `PlayerViewPrefabRequirements`, `DefaultGameplayEntityViewFactory`, `GameplayHostRuntimeFactory`를 새 타입 기준으로 갱신했다.
- `Entity_View_PlayerAnimationTest.prefab`와 관련 테스트 유틸/테스트를 새 component와 field 이름으로 migration했다.
- authoritative timing 검증은 더 이상 prefab authoring이 아니라 `PlayerControlTimingSettings`를 기준으로 유지한다.

### 6-4. 4단계: 공용 actor motion presentation override 도입

목표는 player / enemy 공용 root motion override 계층을 추가하는 것이다.

신규 후보 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EntityMotionPresentationAuthoring.cs`

권장 필드:

- `moveMotionDurationSeconds = -1f`
- `pushMotionDurationSeconds = -1f`
- `flipMotionDurationSeconds = -1f`

작업:

- `GameplayEntityView`에 부착 가능한 공용 presentation authoring 추가
- player prefab과 미래 enemy prefab 모두 같은 규칙으로 사용
- 기본 primitive view에는 미부착 상태를 허용

완료 조건:

- actor-specific root motion override를 공용 authoring으로 표현 가능하다.
- player와 enemy에 같은 구조를 재사용할 수 있다.

### 6-5. 5단계: presentation coordinator를 entity-aware로 변경

목표는 motion duration resolution이 entity별 override를 해석할 수 있도록 만드는 것이다.

수정 대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs`
- 필요 시 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityViewBinder.cs`
- 필요 시 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs`

핵심 변경:

- `ResolveMotionDurationSeconds(TickEntityMotionKind motionKind)`
- 변경 후 `ResolveMotionDurationSeconds(int entityId, TickEntityMotionKind motionKind)`

resolution 순서:

```text
entity motion presentation override
-> global timing profile
```

완료 조건:

- player와 enemy 모두 entity별 move / push / flip motion duration override를 사용할 수 있다.
- override가 없으면 기존 전역값과 동일하게 동작한다.

### 6-6. 6단계: animator duration fallback 정규화

목표는 animator duration과 motion duration의 관계를 명시적으로 고정하는 것이다.

수정 대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimatorDriver.cs`
- 필요 시 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyAnimationAuthoring.cs`

규칙:

- actor animation override가 있으면 그것을 사용
- 없으면 resolved motion duration을 사용
- 그래도 없으면 clip natural length를 사용

완료 조건:

- `push / flip` animator duration의 canonical source가 명확해진다.
- 의도된 override가 아닌 값 드리프트를 제거한다.

### 6-7. 7단계: enemy future extension hook 정리

목표는 지금 당장 enemy animation authoring을 대규모 도입하지 않더라도, 이후 확장 지점을 문서와 코드에서 열어두는 것이다.

작업:

- `EnemyAnimatorDriver`는 여전히 presentation-only로 유지
- 필요 시 future optional authoring 포인트 정의
- `EnemyAiProfile`에는 animation duration을 넣지 않는다는 규칙을 테스트와 문서에서 고정

가능한 future 타입:

- `EnemyAnimationAuthoring`
- `EnemyPresentationProfile`

완료 조건:

- 적별 animation 확장이 필요해져도 `EnemyAiProfile`을 오염시키지 않는다.

### 6-8. 8단계: 씬 / 프리팹 / 테스트 migration

목표는 기존 데이터 자산을 새 구조에 맞춰 이전하는 것이다.

수정 대상 예시:

- `Assets/Scenes/CombinedGameplayShowcase.unity`
- `Assets/Scenes/BoxInteractionShowcase.unity`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Entity_View_PlayerAnimationTest.prefab`
- player test prefab utility
- edit mode / play mode timing tests

작업:

- player prefab의 logic timing 필드 제거에 맞춰 데이터 migration
- scene host configuration이 새 `PlayerControlTimingSettings`를 채우도록 수정
- player presentation duration이 필요한 씬은 animation authoring override로 명시
- drift를 숨기지 말고 의도 여부를 자산에서 드러내기

완료 조건:

- 모든 씬과 prefab이 새 계층 구조를 따른다.
- serialization break 없이 asset이 로드된다.

## 7. 테스트 계획

다음 테스트는 필수다.

### 7-1. ownership guard 테스트

- player prefab이 없어도 authoritative default player logic timing 생성 가능 여부
- player prefab에 logic timing component가 없어도 runtime bootstrap이 가능한지 검증
- `EnemyAiProfile`에 presentation 필드가 추가되지 않았는지 reflection 또는 contract test로 검증

### 7-2. fallback resolution 테스트

- actor motion override 없음 -> global timing 사용
- actor motion override 있음 -> override 사용
- player animation override 없음 -> resolved motion duration 사용
- player animation override 있음 -> animator override 사용

### 7-3. determinism / replay 테스트

- player logic timing source 이동 후에도 replay hash가 안정적인지 검증
- enemy AI logic은 기존과 동일하게 deterministic한지 검증

### 7-4. asset migration 테스트

- 기존 showcase scene 로드 가능 여부
- player prefab validation 통과 여부
- primitive player view 자동 생성 경로에서 새 기본값이 정상 적용되는지 검증

## 8. 구현 순서 권장안

실행 순서는 아래가 안전하다.

1. 문서 추가 (`Gameplay-Timing-Ownership-Reference.md`)
2. `PlayerControlTimingSettings` 도입 및 `GameplayHostRuntimeFactory` player logic timing source 교체 완료
3. `PlayerActionTimingAuthoring` 분리 또는 개명 완료 (`PlayerAnimationTimingAuthoring`)
4. `EntityMotionPresentationAuthoring` 추가
5. `GameplayTickPresentationCoordinator` entity-aware resolution 변경
6. 씬 / 프리팹 migration
7. 테스트 보강

이 순서를 권장하는 이유:

- gameplay authority source를 먼저 안정화해야 이후 presentation migration이 안전하다.
- entity-aware motion resolution은 데이터 migration과 같이 움직여야 회귀를 줄일 수 있다.

## 9. 리뷰 체크리스트

이 구조 이후 timing 관련 PR 리뷰에서는 아래 항목을 확인한다.

- 이 값은 gameplay authority인가 presentation tuning인가
- 이 값은 global default인가 actor-specific override인가
- prefab이 authoritative logic를 소유하고 있지 않은가
- `EnemyAiProfile`에 animation tuning이 들어가 있지 않은가
- `GameplayTimingProfile`에 actor-specific field가 추가되지 않았는가
- motion duration과 animator duration의 fallback 순서가 문서와 일치하는가
- 새 액션 / 새 적 archetype이 기존 계층 규칙을 그대로 따르는가

## 10. 향후 확장 규칙

이 계획이 완료된 이후, 새 timing 요구사항은 아래 절차로 추가한다.

1. gameplay authority인지 presentation인지 먼저 판정
2. actor-specific인지 global default인지 판정
3. 아래 계층 중 정확히 한 곳을 canonical source로 선택

- `GameplayTimingProfile`
- `PlayerControlTimingSettings`
- `EnemyAiProfile`
- `EntityMotionPresentationAuthoring`
- `PlayerAnimationTimingAuthoring`
- `EnemyAnimationAuthoring`

4. 다른 계층에는 override 또는 fallback으로만 연결

새 필드를 추가할 때 같은 의미의 canonical source를 둘 이상 만들면 안 된다.

## 11. 최종 완료 조건

이 계획이 완료되었다고 판단하는 기준은 다음과 같다.

- player authoritative timing이 prefab에서 완전히 제거된다.
- player / enemy motion presentation override가 같은 구조를 사용한다.
- `GameplayTimingProfile`는 global shared timing만 가진다.
- `EnemyAiProfile`는 logic-only profile로 유지된다.
- animator duration과 motion duration의 관계가 코드와 문서에서 일치한다.
- 새 enemy archetype과 새 player action을 추가할 때 어느 계층을 확장해야 하는지 명확하다.

완료 후에는 timing 관련 확장 작업이 "필드가 어디에 들어가야 하는지"로 길게 논쟁되지 않아야 한다.
