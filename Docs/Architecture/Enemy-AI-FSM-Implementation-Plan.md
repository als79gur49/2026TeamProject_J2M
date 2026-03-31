# Enemy AI FSM Implementation Plan

## 1. 목적

이 문서는 `Docs/Architecture/Enemy-AI-FSM-Blueprint.md`를 실제 Unity C# 구현 작업으로 내리기 위한 실행 계획서다.

목표는 "적이 일단 움직이는 프로토타입"이 아니라, 다음 구조 규칙을 먼저 코드 레벨에서 고정하는 것이다.

- 적 판단은 `IEntityLogic` 계층에서 수행한다.
- 적 FSM은 결정론적 상태만 사용한다.
- `Logic -> View` 단방향 구조를 유지한다.
- 애니메이션은 authoritative state가 아니다.
- 예외적 적군도 상태 집합과 policy 조합으로 확장 가능해야 한다.

이 계획서는 다음 문서를 따른다.

- `Docs/Architecture/Hybrid-Architecture-Rulebook.md`
- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
- `Docs/Architecture/Enemy-AI-FSM-Blueprint.md`

## 2. 현재 상태

2026-04-01 기준 현재 코드베이스는 다음 상태다.

- `IEntityLogic` 구조가 이미 존재한다.
- `IMovementEntityLogic`, `IAttackEntityLogic`가 분리되어 있다.
- `WorldState`와 `WorldSnapshot`이 authoritative state와 read facade를 분리하고 있다.
- `TickPresentationData`와 `GameplayTickViewPresenter`가 view 보간과 렌더 지연을 처리한다.
- `EnemyAiMode` authoritative 상태가 `EntityState`, hash, trace, replay dump에 반영되어 있다.
- `EnemyLogic` 골격과 적 로직 자동 materialization 경로가 연결되었다.
- `EnemyAiConfig`와 helper policy 분리로 4단계 진입 전 최소 구조 보정이 반영되었다.
- `aiStateTimer`를 포함한 FSM 상태 전이 commit이 tick pipeline에 연결되었다.

즉, 기반 구조, 적 로직 골격, 생성 경로, 상태 전이 commit까지 1차 수직 슬라이스 핵심 로직이 연결되었고, 다음은 view 연결과 테스트 범위 확대 단계다.

## 3. 구현 원칙

- 적 FSM은 `Animator`, `Transform`, `GameObject`를 참조하지 않는다.
- 적 FSM 상태는 `MonoBehaviour` private field에 두지 않는다.
- 1차 구현은 `EntityState` 소규모 확장 또는 이에 준하는 authoritative record 확장으로 시작한다.
- Blackboard 분리는 1차 목표가 아니라 2차 확장 목표로 둔다.
- 초기 범용화보다 첫 번째 수직 슬라이스를 결정론적으로 완성하는 것을 우선한다.

## 4. 1차 목표

가장 먼저 완성할 범위는 아래 최소 수직 슬라이스다.

- 근접 적 1종
- 상태 집합은 `Patrol -> Chase -> Attack -> Recover`
- 플레이어 감지 규칙 1종
- 1칸 이동 또는 정지 판단
- 근접 단일 타격 1종
- 공격 후 cooldown 처리
- view는 기존 `GameplayTickViewPresenter` 기반 보간을 그대로 사용
- animation은 optional presentation mapper로만 연결

이 범위만으로도 아래 핵심 제약을 검증할 수 있다.

- 적 FSM이 snapshot만 읽는가
- 적 FSM이 intent만 생산하는가
- AI 상태가 replay와 determinism hash에 반영되는가
- animation 없이도 결과가 성립하는가
- `Patrol`, `Chase`, `Attack`, `Recover` 전이가 테스트 가능한가

## 5. 1차 비범위

아래 기능은 1차 범위에 포함하지 않는다.

- Blackboard 별도 저장소 도입
- 경로 탐색 기반 추적
- 복수 타겟 우선순위
- ranged enemy
- 자폭 유닛
- contact damage 전용 유닛
- 포탑형 적
- animation event 기반 보조 연출
- enemy-specific VFX authoring

## 6. 권장 구현 전략

1차 구현은 `EntityState` 확장 기반으로 시작한다.

이유:

- 현재 snapshot, hash, debug dump 구조에 가장 자연스럽게 들어간다.
- 변경 범위를 통제하기 쉽다.
- 첫 번째 적 FSM을 빠르게 검증할 수 있다.

초기 추가 후보:

- `EnemyAiMode aiMode`
- 필요 시 `int targetEntityId`

초기에는 아래 항목을 바로 추가하지 않는다.

- `lastSeenPosition`
- `patrolIndex`
- `suspicion`
- `pathCursor`

이 값들은 적 종류가 늘어날 때 Blackboard 후보로 남겨둔다.

## 7. 단계별 작업 계획

### 7-1. 1단계: AI 상태 표현 추가

2026-03-31 구현 완료.

목표는 적 FSM이 사용할 최소 authoritative 상태를 추가하는 것이다.

작업:

- `EnemyAiMode` enum 추가
- `EntityState` 또는 대응 authoritative entity record에 최소 AI 상태 추가
- snapshot 생성 경로가 새 필드를 보존하는지 확인
- determinism hash와 trace dump가 새 필드를 반영하도록 정리

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/EntityState.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAiMode.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/DeterminismHashBuilder.cs`
- 필요 시 debug trace 관련 파일

완료 조건:

- 적 AI 상태가 authoritative data로 저장된다.
- 같은 initial state면 같은 AI 상태로 복원된다.

구현 메모:

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAiMode.cs` 추가
- `EntityState`에 최소 authoritative AI 필드로 `aiMode` 추가
- `DeterminismHashBuilder`, `TickTraceFormatter`, replay/fuzz entity dump에 `aiMode` 반영
- replay test에 snapshot 보존 및 hash 반영 검증 추가
- `targetEntityId`는 2단계 이후 실제 센서/타겟팅 규칙이 들어갈 때 추가 여부를 재평가

### 7-2. 2단계: 적 Logic 뼈대 추가

2026-03-31 구현 완료.

목표는 적 FSM을 `IEntityLogic` 구조 위에 올리는 것이다.

작업:

- `EnemyLogic` 추가
- `IMovementEntityLogic`, `IAttackEntityLogic`, `IEntityLogicSourceBinding` 구현
- snapshot 기반 센서 로직 추가
- FSM 상태별 intent 생성 분기 추가

대상 파일:

- 신규 `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs`

완료 조건:

- 적 1개가 tick마다 자신의 상태를 읽고 이동 또는 공격 intent를 생산할 수 있다.

구현 메모:

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs` 추가
- `EnemyLogic`이 `IMovementEntityLogic`, `IAttackEntityLogic`, `IEntityLogicSourceBinding`를 함께 구현
- snapshot 센서가 가장 가까운 적대 `Unit`을 결정론적으로 선택
- `Patrol`은 정면 1칸 이동, `Chase`는 타겟 추적 1칸 이동, `Attack`은 인접 타겟에 근접 공격 intent 생성, `Recover/Dead/None`은 no-op으로 정리
- `targetEntityId` 저장은 아직 도입하지 않고 tick snapshot에서 재탐색
- 단위 테스트로 patrol/chase/attack/recover 분기와 chase fallback 동작을 고정

### 7-3. 3단계: 적 Logic 생성 경로 연결

2026-04-01 구현 완료.

목표는 런타임에서 적 엔티티에 대응하는 logic을 자동 조립하는 것이다.

작업:

- `EnemyEntityLogicFactory` 추가
- 적 엔티티 타입 또는 식별 규칙 정의
- `GameplayEntityLogicProviderFactory` 또는 host configuration 경로에 적 logic 등록

대상 파일:

- 신규 `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyEntityLogicFactory.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/GameplayEntityLogicProviderFactory.cs`
- 필요 시 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs`

완료 조건:

- world snapshot에 적 엔티티가 존재하면 대응하는 `EnemyLogic`이 materialize된다.

구현 메모:

- 신규 `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyEntityLogicFactory.cs` 추가
- 적 엔티티 식별 규칙을 `EntityType.Unit && aiMode != EnemyAiMode.None`으로 고정
- `GameplayEntityLogicProviderFactory.CreateDefault()`에 `EnemyEntityLogicFactory` 등록
- 기본 composition root 경로에서 적 patrol/attack logic이 자동 materialize되는 edit mode 테스트 추가

### 7-4. 4단계: 상태 전이와 행동 규칙 완성

2026-04-01 구현 완료.

목표는 첫 번째 표준 적 FSM을 실제로 완성하는 것이다.

작업:

- `Patrol` 규칙 정의
- `Chase` 규칙 정의
- `Attack` 규칙 정의
- `Recover` 규칙 정의
- 도입된 `EnemyAiConfig` 값을 상태 전이 규칙과 실제 intent/commit 흐름에 연결

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAiConfig.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyTargetSelector.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyMovementPolicy.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyCombatPolicy.cs`

완료 조건:

- 적이 감지 전에는 배회한다.
- 감지 후에는 추적한다.
- 사거리 진입 시 공격한다.
- 공격 후 recover 상태를 거친다.

4단계 진입 전 구조 보정 메모:

- `EnemyAiConfig`를 먼저 도입해 감지 범위, 공격 범위, priority, recover tick을 상수 대신 rule data로 이동
- `EnemyLogic`은 authoritative 상태 조회, mode 분기, helper 조합만 담당하는 coordinator로 유지
- 결정론적 타겟 선택은 `EnemyTargetSelector`, 이동 규칙은 `EnemyMovementPolicy`, 공격 intent 생성은 `EnemyCombatPolicy`로 분리
- 적 이동 경로는 `WorldSnapshot.TryResolvePlayerStep(...)`에 직접 묶지 않고 의미 중립 alias인 `TryResolveUnitStep(...)`를 사용

구현 메모:

- `EntityState`에 `aiStateTimer`를 추가해 `Recover` cooldown을 AI authoritative state로 보존
- `WorldState`/`WorldStateWriteContext`/`IWorldStateMutationPort`에 `ApplyEnemyAiState(...)` commit 경로 추가
- `TickPipeline`에 `BeforeMovement`, `BeforeAttack`, `AfterAttack` AI 전이 시점을 추가해 same-tick 전이와 intent 생산을 연결
- `EnemyLogic`은 `IEnemyAiStateLogic`을 함께 구현하고, 전이 규칙은 `EnemyAiStateResolver` helper로 분리
- `Patrol -> Chase`, `Chase -> Attack`, `Attack -> Recover`, `Recover -> Chase/Patrol` 전이를 authoritative commit으로 고정
- trace, replay dump, determinism hash에 `AiTimer`를 반영
- unit/replay 테스트로 전이, recover countdown, hash 반영을 검증

### 7-5. 5단계: View 연결

목표는 logic을 건드리지 않고 view가 적 상태를 해석하게 하는 것이다.

작업:

- 적 presentation mapper 추가
- 필요 시 `EnemyAnimatorDriver` 추가
- 이동, 공격, 피격, 사망을 view가 `TickResult` 또는 presentation state 기반으로 재생하게 연결

대상 파일:

- 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyAnimatorDriver.cs`
- 필요 시 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyViewPresentationMapper.cs`

완료 조건:

- animation 유무와 관계없이 logic 결과가 동일하다.
- animation은 logic 전이에 영향을 주지 않는다.

### 7-6. 6단계: 테스트 고정

목표는 적 FSM 구조를 테스트로 고정하는 것이다.

작업:

- 단위 테스트 추가
- scenario 테스트 추가
- determinism/replay 테스트 추가

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/`

완료 조건:

- 적 FSM 전이와 intent 생성이 테스트로 보호된다.
- view 유무가 tick 결과를 바꾸지 않음이 보장된다.

## 8. 권장 타입 구조

초기 target-state 기준 권장 타입 구조는 다음과 같다.

```text
Assets/_Features/Gameplay/
  Gameplay_Entities/
    Runtime/
      EnemyAiMode.cs
      EnemyAiConfig.cs
      EnemyTargetSelector.cs
      EnemyMovementPolicy.cs
      EnemyCombatPolicy.cs
      EnemyLogic.cs
      EnemyEntityLogicFactory.cs
  Gameplay_Host/
    Runtime/
      EnemyAnimatorDriver.cs
      EnemyViewPresentationMapper.cs
```

규칙:

- 적 판단은 `Gameplay_Entities`에 둔다.
- view 해석은 `Gameplay_Host`에 둔다.
- 적 FSM이 `Gameplay_Host`를 참조하지 않게 한다.

## 9. 테스트 계획

### 9-1. Unit

- 타겟이 없으면 `Patrol` 유지
- 타겟 감지 시 `Chase` 전이
- 공격 가능 거리 진입 시 attack intent 생성
- recover 중에는 공격 intent 미생성
- 타겟 상실 시 `Patrol` 복귀
- recover countdown 종료 후 `Chase` 재진입

### 9-2. Scenario

- 실제 tick 진행에서 `Patrol -> Chase -> Attack -> Recover` 순서 검증
- 플레이어 위치 변화에 따른 상태 전이 검증
- 적 사망 후 cleanup 처리 검증

### 9-3. Replay

- 동일한 initial state와 input에서 동일한 최종 snapshot 보장
- 적 AI 상태가 determinism hash에 반영됨을 검증
- `aiStateTimer`가 trace/replay dump/hash에 반영됨을 검증

## 10. 2차 확장 계획

1차 적이 안정화된 뒤 아래 순서로 확장한다.

1. 배회 전용 적
2. 공격 없는 추적 적
3. 충돌 피해 적
4. 고정 포탑
5. 자폭 적

이 시점에 아래 조건이 충족되면 Blackboard 분리를 검토한다.

- 적 종류가 4종 이상으로 늘어난다.
- `EntityState`에 AI 관련 필드가 계속 누적된다.
- 적마다 필요한 내부 기억 구조가 뚜렷하게 달라진다.

## 11. Blackboard 도입 조건

아래 중 둘 이상이 발생하면 2차 리팩터링 대상으로 본다.

- `EntityState`에 AI 전용 필드가 4개 이상 누적된다.
- 특정 적 전용 필드가 공통 엔티티 상태를 오염시키기 시작한다.
- 순찰 인덱스, 마지막 목격 위치, 경계 수치 같은 내부 기억이 늘어난다.
- 적 종류별로 완전히 다른 메모리 구조가 필요해진다.

이 경우 다음 작업이 필요하다.

- `EnemyAiBlackboardState` 정의
- `WorldState` 내부 저장소 추가
- snapshot read path 추가
- commit path 추가
- determinism hash 직렬화 추가
- entity 제거 시 blackboard cleanup 추가

## 12. 완료 기준

이 계획서 기준 1차 구현 완료는 다음 상태를 의미한다.

- 적 AI가 `IEntityLogic`로 구현되어 있다.
- FSM 상태가 authoritative data로 유지된다.
- animation 없이도 적 행동이 완결된다.
- `Logic -> View` 단방향 구조가 유지된다.
- 첫 번째 표준 적이 테스트와 replay 기준을 통과한다.

## 13. 최종 결론

현재 프로젝트에서 가장 현실적인 구현 경로는 다음이다.

1. `EntityState` 소규모 확장
2. `EnemyLogic` 도입
3. 표준 근접 적 1종 완성
4. 테스트 고정
5. 적 종류 증가 시 Blackboard 분리 검토

즉, 첫 구현은 단순하게 시작하되, 장기적으로는 Blackboard로 넘어갈 수 있게 경계를 열어 두는 방식이 가장 안전하다.
