# Deterministic Tick Simulation Implementation Plan

## 1. 목적

이 문서는 `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`를 실제 Unity C# 구현 작업으로 내리기 위한 실행 계획서다.

목표는 "빨리 돌아가는 전투 프로토타입"이 아니라, 다음 불변 규칙을 코드 레벨에서 먼저 고정하는 것이다.

- `WorldState`만 authoritative state다.
- `EntityLogic`은 읽기 전용 intent 생산자다.
- 실제 월드 변경은 Committer만 수행한다.
- Tick 순서는 `Movement -> Attack -> Cleanup`으로 고정한다.
- 모든 ID는 수집 후 정렬 뒤 중앙에서 발급한다.
- View는 Tick 내부를 모르고 `TickResult`만 소비한다.

이 계획서는 `Docs/Architecture/Hybrid-Architecture-Rulebook.md`와 `AI_HYBRID_STRUCTURE_RULES.md`를 따른다. 따라서 구현 코드는 `Assets/_Features/Gameplay` 아래에 배치한다.

## 2. 구현 범위

### 2-1. 1차 목표

가장 먼저 완성할 범위는 최소 수직 슬라이스다.

- 엔티티 2개만 존재
- `Unit`만 존재
- 1칸 이동만 지원
- 근접 단일 데미지 1회만 지원
- 사망 시 Cleanup에서 제거
- Spawn 없음
- Projectile 없음
- Push 없음

이 범위만으로도 아래 핵심 제약을 검증할 수 있다.

- `EntityLogic`이 Intent만 생산하는가
- Resolver가 선택만 하고 수정을 하지 않는가
- Committer만 `WorldState`를 쓰는가
- `hp <= 0` 엔티티가 Cleanup 전까지 월드에 남는가
- Snapshot 기준 판정이 Tick 내내 유지되는가
- 같은 입력에서 같은 `intentId`, `groupId`, 결과 해시가 나오는가

### 2-2. 1차 비범위

아래 기능은 뼈대 고정 이후에 연다.

- PushChain
- Projectile 이동
- `ImpactReservation`
- Spawn
- 상태 이상 세분화
- 반격, on-hit 연쇄, 즉시 reaction
- edge reservation
- 고속 경로 판정

## 3. 구조 고정 원칙

### 3-1. 코드 차원의 강제 방식

설계 규칙을 "합의"가 아니라 "구조"로 강제한다.

- `WorldState` 내부 컬렉션은 `private`로 감춘다.
- 읽기 계층은 `WorldSnapshot`만 받는다.
- 쓰기 계층은 `IWorldWriteContext`만 받는다.
- `TickPipeline`만 `WorldStateWriteContext`를 생성해서 Committer에 전달한다.
- `EntityLogic`, Expander, Resolver는 `WorldState` 참조를 받지 않는다.
- Phase 경계는 `RunMovementPhase`, `RunAttackPhase`, `RunCleanupPhase` 메서드로 분리한다.
- ID 발급은 `IdAllocator` 한 곳에서만 수행한다.
- 정렬은 전용 comparer로만 수행한다.
- `GetHashCode()` 기반 결정론 검증은 금지한다.

### 3-2. 기술적 강제 규칙

- `Dictionary`, `HashSet` 순회 결과를 직접 의미론에 사용하지 않는다.
- `List.Sort` 안정성에 의존하지 않는다.
- 정렬 키는 항상 총정렬이 되도록 끝까지 채운다.
- raw intent, raw candidate에는 ID를 붙이지 않는다.
- Committer 외 계층에서 `Remove`, `Move`, `Damage`, `Spawn`, `MarkDestroy`를 호출할 수 없게 한다.
- Cleanup 전에 occupancy 제거를 하지 않는다.

## 4. 권장 폴더 구조

초기 구현은 `Game.Feature.Gameplay` 단일 runtime asmdef 하나로 시작하고, 테스트 asmdef만 별도로 둔다. 지금 단계에서는 어셈블리 세분화보다 구조 규칙 고정이 우선이다.

```text
Assets/_Features/Gameplay/
  Gameplay.asmdef
  Gameplay_Model/
    Runtime/
      Phases/
        TickPhase.cs
      Intents/
        Intent.cs
      Actions/
        MoveAction.cs
        DamageAction.cs
        DestroyAction.cs
        SpawnAction.cs
        StateChangeAction.cs
      Groups/
        ActionGroup.cs
        ActionGroupKind.cs
      Sorting/
        IntentComparer.cs
        ActionGroupComparer.cs
  Gameplay_Loop/
    Runtime/
      TickPipeline.cs
      TickRunner.cs
      TickInput.cs
      TickInputBuffer.cs
      TickResult.cs
      TickResultBuilder.cs
      IdAllocator.cs
      PhaseTransientBuffer.cs
      DeterminismHashBuilder.cs
  Gameplay_BoardState/
    Runtime/
      EntityState.cs
      EntityType.cs
      EntityPhaseState.cs
      Direction.cs
      TerrainData.cs
      WorldState.cs
      WorldSnapshot.cs
      SnapshotBuilder.cs
      WorldQueryService.cs
      WorldStateWriteContext.cs
      IWorldWriteContext.cs
  Gameplay_Entities/
    Runtime/
      IEntityLogic.cs
      PlayerLogic.cs
      EnemyLogic.cs
  Gameplay_Movement/
    Runtime/
      Intents/
        MoveIntent.cs
      Collection/
        RawMovementIntent.cs
        MovementIntentCollector.cs
      Expansion/
        MovementExpander.cs
      Resolution/
        MovementResolver.cs
        MovementConflictDetector.cs
      Commit/
        MovementCommitter.cs
  Gameplay_Attack/
    Runtime/
      Intents/
        AttackIntent.cs
      Collection/
        RawAttackIntent.cs
        AttackIntentCollector.cs
      Expansion/
        AttackExpander.cs
      Resolution/
        AttackResolver.cs
      Commit/
        AttackCommitter.cs
  Gameplay_Cleanup/
    Runtime/
      CleanupProcessor.cs
      RemovalProcessor.cs
      StateTimerProcessor.cs
      StateTransitionProcessor.cs
  Gameplay_Debug/
    Runtime/
      TickTrace.cs
      TickTraceBuilder.cs
      TickTraceFormatter.cs
  Gameplay_Tests/
    EditMode/
      Gameplay.Tests.asmdef
      Unit/
      Scenario/
      Replay/
      Fuzz/
```

현재 구현 기준으로 phase 공용 실행 모델은 `Gameplay_Model`에 둔다.

- `Intent`, `ActionGroup`, `TickPhase`는 특정 feature가 아니라 공용 실행 모델 소유다.
- `MoveIntent`, `AttackIntent` 같은 concrete intent만 각 feature 레이어에 둔다.
- `IntentComparer`, `ActionGroupComparer`는 feature 전용 비교기가 아니라 공용 정렬 계약이다.
- `MoveAction`, `DamageAction`, `DestroyAction`, `SpawnAction`, `StateChangeAction`도 `ActionGroup`과 함께 공용 모델로 관리한다.

### 4-1. 네임스페이스 규칙

- 루트 네임스페이스는 `Game.Feature.Gameplay`
- 하위는 폴더 기준으로 나눈다
- 예:
  - `Game.Feature.Gameplay.Loop`
  - `Game.Feature.Gameplay.BoardState`
  - `Game.Feature.Gameplay.Movement`
  - `Game.Feature.Gameplay.Attack`
  - `Game.Feature.Gameplay.Cleanup`

## 5. 핵심 타입 설계 계획

### 5-1. 반드시 먼저 만드는 타입

초기 스캐폴딩에서 아래 타입부터 고정한다.

- `EntityState`
- `Intent`
- `ActionGroup`
- `WorldState`
- `WorldSnapshot`
- `TickInput`
- `TickResult`
- `PhaseTransientBuffer`
- `IdAllocator`

### 5-2. WorldState와 쓰기 경계

`WorldState`는 상태 저장소이고, 직접 수정용 public API를 노출하지 않는다.

권장 구조:

- `WorldState`
  - `private Dictionary<int, EntityState> entitiesById`
  - `private Dictionary<Vector2Int, int> unitOccupancy`
  - `private Dictionary<Vector2Int, int> projectileOccupancy`
  - `private TerrainData terrainData`
- `WorldStateWriteContext`
  - `MoveEntity`
  - `ApplyDamage`
  - `ApplyStateChange`
  - `MarkDestroy`
  - `SpawnEntity`
  - `RemoveEntity`
  - `SetFacing`

핵심은 "월드 전체 객체를 넘겨주지 말고, 쓰기 capability만 Committer에 넘긴다"는 점이다.

### 5-3. WorldSnapshot와 질의 함수

초기 구현에서 아래 질의 API를 인터페이스처럼 먼저 고정한다.

- `TryGetEntity(int entityId, out EntityState entity)`
- `TryGetUnitAt(Vector2Int cell, out EntityState entity)`
- `TryGetProjectileAt(Vector2Int cell, out EntityState entity)`
- `IsBlockedForUnit(Vector2Int cell)`
- `BlocksMovement(int entityId)`
- `CanBeTargetedForNewSelection(int entityId)`
- `EnumerateEntitiesOrdered(List<EntityState> buffer)`

중요 포인트:

- Snapshot은 생성 후 절대 변경하지 않는다.
- 외부는 occupancy 딕셔너리를 직접 순회하지 않는다.
- 질의 정책은 중앙 함수에서만 계산한다.

### 5-4. Intent와 ActionGroup

초기 버전에서는 Phase마다 source당 하나의 primary intent만 허용한다.

권장 필드:

- `Intent`
  - `int intentId`
  - `int sourceId`
  - `int priority`
  - `TickPhase phase`
- `ActionGroup`
  - `int groupId`
  - `int intentId`
  - `int sourceId`
  - `int priority`
  - `ActionGroupKind groupKind`
  - `List<MoveAction> moves`
  - `List<DamageAction> damages`
  - `List<StateChangeAction> stateChanges`
  - `List<DestroyAction> destroys`

초기 1차 슬라이스에서는 `SpawnAction`과 projectile 관련 action은 비워 둔다.

### 5-5. IdAllocator

`IdAllocator`는 Tick 파이프라인 중앙에서만 사용한다.

초기 API:

- `ResetForTick(int tickIndex)`
- `AllocateIntentId()`
- `AllocateGroupId()`
- `AllocateSpawnId()`

추가 원칙:

- raw collection 단계에서 ID를 발급하지 않는다.
- replay test에서는 발급 순서를 trace에 남긴다.

## 6. TickPipeline 상세 계획

`TickPipeline`은 우회 경로가 없는 고정 파이프라인이어야 한다.

```csharp
public sealed class TickPipeline
{
    public TickResult RunTick(in TickInput input);

    private MovementPhaseResult RunMovementPhase(
        in WorldSnapshot snapshot,
        in TickInput input,
        PhaseTransientBuffer transientBuffer);

    private AttackPhaseResult RunAttackPhase(
        in WorldSnapshot snapshot,
        PhaseTransientBuffer transientBuffer);

    private CleanupPhaseResult RunCleanupPhase();
}
```

### 6-1. Tick 실행 순서

1. `IdAllocator.ResetForTick`
2. `TickTrace` 시작
3. `SnapshotBuilder.Create(worldState)`로 `S0` 생성
4. `RunMovementPhase(S0, input, transientBuffer)`
5. `SnapshotBuilder.Create(worldState)`로 `S1` 생성
6. `RunAttackPhase(S1, transientBuffer)`
7. `RunCleanupPhase()`
8. `TickResultBuilder.Build`
9. `DeterminismHashBuilder.Build`
10. `TickTrace` 종료

### 6-2. Phase 결과 타입

Phase 간 디버깅과 테스트를 위해 결과 타입을 분리한다.

- `MovementPhaseResult`
  - sorted intents
  - expanded candidates
  - selected groups
  - commit events
- `AttackPhaseResult`
  - sorted inputs
  - expanded candidates
  - selected groups
  - commit events
- `CleanupPhaseResult`
  - removed ids
  - timer changes
  - state transitions

이 결과 객체는 View용이 아니라, trace와 테스트용이다.

## 7. 단계별 구현 계획

## 7-1. 단계 0: 구조 뼈대 고정

산출물:

- 폴더/asmdef 생성
- 네임스페이스 정책 고정
- `TickPhase`, `TickInput`, `TickResult` 빈 타입 생성
- `TickPipeline` 메서드 뼈대 생성
- `IEntityLogic`, `IWorldWriteContext` 시그니처 고정

완료 기준:

- 코드가 아직 기능을 거의 안 해도 `RunTick` 호출 경로가 존재한다.
- Phase 진입/종료 로그가 남는다.
- 구조적으로 Committer 외 월드 쓰기 경로가 없다.

## 7-2. 단계 1: 도메인 골격 구현

산출물:

- `EntityState`
- `WorldState`
- `WorldSnapshot`
- `SnapshotBuilder`
- `WorldQueryService`
- `Intent`, `ActionGroup`
- `IdAllocator`
- `PhaseTransientBuffer`

완료 기준:

- Snapshot이 불변으로 동작한다.
- 중앙 질의 함수가 준비된다.
- intent/group ID를 중앙에서 발급할 수 있다.

## 7-3. 단계 2: 최소 Movement 수직 슬라이스

지원 범위:

- `MoveIntent`
- 1칸 이동
- blocked면 실패
- 동일 목적지 충돌 시 하나만 선택
- `Stop` fallback 허용 여부는 문서로 고정

구현 순서:

1. `MovementIntentCollector`
2. raw intent dump
3. `IntentComparer`
4. post-sort `intentId` 발급
5. `MovementExpander`
6. raw candidate dump
7. `ActionGroupComparer`
8. post-sort `groupId` 발급
9. `MovementResolver`
10. `MovementCommitter`
11. occupancy before/after dump

완료 기준:

- 빈 칸 이동 성공
- 막힌 칸 이동 실패
- 같은 목적지 경쟁에서 결정론적으로 하나만 선택
- 같은 입력 두 번 실행 시 같은 trace/hash

## 7-4. 단계 3: 최소 Attack 수직 슬라이스

지원 범위:

- `AttackIntent`
- 인접 대상 단일 데미지
- `StateChange -> Damage -> DestroyMark`만 구현
- Spawn 없음
- reservation 없음

구현 순서:

1. `AttackIntentCollector`
2. raw attack intent dump
3. `IntentComparer`
4. post-sort `intentId` 발급
5. `AttackExpander`
6. `ActionGroupComparer`
7. post-sort `groupId` 발급
8. `AttackResolver`
9. `AttackCommitter`

완료 기준:

- 살아 있는 엔티티만 Attack Intent 생성
- 공격 중 사망해도 Cleanup 전까지 occupancy 유지
- 같은 Tick 내 이미 생성된 attack candidate는 유지

## 7-5. 단계 4: Cleanup 구현

산출물:

- `CleanupProcessor`
- `RemovalProcessor`
- `StateTimerProcessor`
- `StateTransitionProcessor`

고정 순서:

1. `hp <= 0` 또는 `DestroyMark` 제거 대상 확정
2. occupancy 갱신
3. `stateTimer` 감소
4. 상태 전이
5. cleanup 이벤트 기록

완료 기준:

- 제거는 Cleanup에서만 발생
- 생성 Tick의 Spawn entity는 timer 감소 제외
- Cleanup 결과가 같은 Tick의 판정에 역류하지 않음

## 7-6. 단계 5: TickResult, Trace, Replay

산출물:

- `TickResultBuilder`
- `TickTraceBuilder`
- `DeterminismHashBuilder`
- replay harness

완료 기준:

- 매 Tick trace 파일 또는 문자열 생성
- 매 Tick 짧은 hash 생성
- 동일 입력 replay 시 동일 hash 보장

## 7-7. 단계 6: reservation과 확장 기능

다음 순서로 연다.

1. `ImpactReservation`
2. Attack input 정규화
3. Projectile movement
4. Spawn
5. PushChain
6. edge reservation
7. on-hit 확장 검토

원칙:

- 확장 기능도 기존 `Intent -> Expand -> Resolve -> Commit` 경로에만 합류시킨다.
- Commit 중간 새 Intent 생성은 계속 금지한다.

## 8. 상세 Phase 설계

### 8-1. Movement Phase

초기 메서드 체인:

```text
CollectMovementIntents
-> SortMovementIntents
-> AssignMovementIntentIds
-> ExpandMovementCandidates
-> SortMovementCandidates
-> AssignMovementGroupIds
-> ResolveMovement
-> CommitMovement
```

강제 규칙:

- 수집 단계에서는 ID 없음
- 정렬 후에만 ID 발급
- Resolver는 후보 수정 금지
- Committer는 선택된 group만 적용

### 8-2. Attack Phase

초기 메서드 체인:

```text
CollectAttackIntents
-> SortAttackIntents
-> AssignAttackIntentIds
-> ExpandAttackCandidates
-> SortAttackCandidates
-> AssignAttackGroupIds
-> ResolveAttack
-> CommitAttack
```

확장 단계 메서드 체인:

```text
CollectAttackIntents
-> DrainImpactReservations
-> NormalizeAttackInputs
-> SortAttackInputs
-> AssignAttackIntentIds
-> ExpandAttackCandidates
-> ResolveAttack
-> CommitAttack
```

강제 규칙:

- Attack 시작 시 살아 있는 엔티티만 intent 생성 가능
- damage 누적은 허용
- destroy 중복은 정리
- 실제 remove는 하지 않음

### 8-3. Cleanup Phase

초기 메서드 체인:

```text
CollectRemovalTargets
-> RemoveEntities
-> RebuildOccupancyIfNeeded
-> TickStateTimers
-> ApplyStateTransitions
-> BuildCleanupEvents
```

강제 규칙:

- remove는 Cleanup에서만
- `stateTimer == 0` 효력은 다음 Tick부터
- Cleanup 결과는 같은 Tick에서 다시 읽지 않음

## 9. 테스트 계획

테스트는 `단위 -> 시나리오 -> 리플레이 -> 퍼즈` 순서로 쌓는다.

### 9-1. 단위 테스트

우선순위 높은 단위 테스트:

- `IdAllocator`가 같은 입력에서 같은 순서의 ID를 발급하는가
- `WorldSnapshot`이 생성 후 불변처럼 동작하는가
- `BlocksMovement`가 Cleanup 전까지 true를 유지하는가
- `CanBeTargetedForNewSelection`가 `markedForDeath` 정책을 따르는가
- `IntentComparer`와 `ActionGroupComparer`가 총정렬을 보장하는가
- `PhaseTransientBuffer.DrainImpacts()`가 결정론적 순서를 유지하는가

### 9-2. 시나리오 테스트

초기 시나리오 이름을 문서 규칙과 1:1 대응되게 만든다.

- `Movement_EmptyCellMove_Succeeds`
- `Movement_BlockedCellMove_Fails`
- `Movement_SameDestination_OnlyHigherPriorityWins`
- `Movement_SameInput_AssignsDeterministicIntentIds`
- `Attack_MoveThenAttack_UsesPostMoveSnapshot`
- `Attack_DeadAfterDamage_StillOccupiesUntilCleanup`
- `Cleanup_DestroyMarkedEntity_IsRemovedOnlyInCleanup`

### 9-3. 리플레이 테스트

기록 항목:

- 초기 world seed
- TickInput 시퀀스
- 매 Tick hash
- 매 Tick trace 요약

비교 항목:

- `entitiesById` 정렬 덤프
- occupancy 정렬 덤프
- `markedForDeath` 정렬 덤프
- `TickResult` 이벤트 정렬 덤프

주의:

- .NET 기본 `GetHashCode()`는 사용하지 않는다.
- 문자열 기반 canonical dump 뒤에 FNV-1a 64bit 같은 고정 해시를 적용한다.

### 9-4. 퍼즈 테스트

랜덤 seed 기반으로 아래를 생성한다.

- 유닛 수
- 초기 위치
- 입력 방향
- 공격 대상
- 우선순위

검증:

- 같은 seed로 2회 실행 시 모든 Tick hash가 동일해야 한다.
- divergence가 생기면 해당 Tick trace를 자동 보관한다.

## 10. 디버깅 도구 계획

### 10-1. Tick Trace

매 Tick 아래 항목을 남긴다.

- `S0` snapshot 요약
- movement raw intents
- movement sorted intents
- movement candidates
- rejected reason
- selected groups
- occupancy before/after
- `S1` snapshot 요약
- attack raw or normalized inputs
- attack candidates
- attack selected groups
- cleanup removed ids
- final `TickResult`

### 10-2. Determinism Hash

포맷 예시:

```text
Tick 00152 | Hash 7A31E2D4
```

필수 조건:

- 정렬된 canonical dump에서 계산
- 자료구조 메모리 주소나 런타임 종속 값 사용 금지

### 10-3. 개발용 디버그 뷰

런타임 디버그 UI는 개발용으로만 분리한다.

- unit occupancy
- projectile occupancy
- `markedForDeath`
- `stateTimer`
- selected `ActionGroup`
- rejected reason
- last hash

원칙:

- 프로덕션 View는 여전히 `TickResult`만 소비한다.
- 디버그 전용 화면만 내부 상태를 직접 읽을 수 있다.

## 11. 코드 리뷰 체크리스트

아래 질문을 PR 체크리스트로 고정한다.

- `EntityLogic`이 `WorldState` live data를 직접 읽는가
- Committer 외 클래스가 `WorldState`를 수정하는가
- raw intent 수집 시점에 ID를 발급했는가
- `Dictionary`/`HashSet` 순회 결과에 의미를 부여했는가
- 정렬 키가 총정렬을 보장하는가
- Cleanup 전에 remove를 수행했는가
- Attack 중 사망한 엔티티를 즉시 occupancy에서 제거했는가
- View 코드가 Tick 내부 타이밍에 개입하는가
- 신규 기능이 기존 Phase 순서를 우회하는가

## 12. 각 단계의 완료 정의

### 12-1. 구조 단계 완료

- `RunTick`이 빈 Phase라도 끝까지 돈다.
- `TickPipeline` 외 우회 실행 경로가 없다.
- 테스트 프로젝트가 생성되어 있다.

### 12-2. 최소 전투 슬라이스 완료

- 이동과 공격, Cleanup이 한 Tick 흐름으로 연결된다.
- `hp <= 0` 제거 시점이 Cleanup으로 고정된다.
- trace와 hash가 남는다.

### 12-3. 확장 준비 완료

- replay test가 통과한다.
- scenario test가 핵심 규칙을 덮는다.
- reservation 추가를 위한 `PhaseTransientBuffer` 자리가 이미 존재한다.

## 13. 실제 착수 순서

처음 착수는 아래 순서로 진행한다.

1. `Gameplay.asmdef`와 테스트 asmdef 생성
2. `TickPipeline`, `TickInput`, `TickResult`, `TickPhase` 빈 뼈대 생성
3. `EntityState`, `WorldState`, `WorldSnapshot`, `SnapshotBuilder`, `IdAllocator` 작성
4. `IWorldWriteContext`와 `WorldStateWriteContext`로 쓰기 경계 고정
5. `IEntityLogic`, `PlayerLogic`, `EnemyLogic` 빈 구현 추가
6. 최소 `MoveIntent` 수집부터 Movement 수직 슬라이스 완성
7. 최소 `AttackIntent` 수직 슬라이스 완성
8. Cleanup, trace, hash, replay 테스트 추가
9. 그 다음 reservation, projectile, spawn 순으로 확장

## 14. 한 줄 구현 원칙

가장 중요한 구현 원칙은 아래 한 줄이다.

> 먼저 뼈대를 잠그고, 최소 수직 슬라이스를 끝까지 관통시킨 뒤, 로그와 리플레이 테스트로 결정론을 고정한 다음에만 복잡도를 연다.
