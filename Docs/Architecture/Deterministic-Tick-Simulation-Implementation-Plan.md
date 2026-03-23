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

문서 해석 규칙:

- 블루프린트는 안정된 책임 경계와 설계 원칙을 정의한다.
- 이 구현 계획서는 `current-state`와 `target-state`를 함께 관리한다.
- Unity generated `.csproj`가 explicit compile include를 쓰기 때문에, current-state는 일부 타입을 기존 `.cs` 파일에 co-locate할 수 있다.
- 따라서 파일 개수보다 `namespace`, `type boundary`, `구체 책임`을 우선 기준으로 본다.

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

아래 트리는 target-state 기준이다.

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
      GameplayCompositionRoot.cs
      GameplayBootstrapper.cs
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
      IEntityLogicFactory.cs
      ISnapshotEntityLogicProvider.cs
      IEntityLogicSourceBinding.cs
      SnapshotEntityLogicProvider.cs
      GameplayEntityLogicProviderFactory.cs
      PlayerLogic.cs
      EnemyLogic.cs
      ProjectileEntityLogicFactory.cs
      ProjectileLogic.cs
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

current-state 메모:

- 현재 구현은 generated `.csproj` explicit include 제약 때문에 일부 타입을 기존 파일에 co-locate한다.
- 현재 기준 co-location은 아래와 같다.
  - `Gameplay_Loop/Runtime/TickPipeline.cs`
    - `TickPipeline`
    - `SnapshotEntityLogicProvider`
    - `GameplayEntityLogicProviderFactory`
    - `GameplayBootstrapper`
    - `GameplayCompositionRoot`
  - `Gameplay_Entities/Runtime/IEntityLogic.cs`
    - `IEntityLogic`
    - `IEntityLogicSourceBinding`
    - `IEntityLogicFactory`
    - `ISnapshotEntityLogicProvider`
  - `Gameplay_Entities/Runtime/ProjectileLogic.cs`
    - `ProjectileLogic`
    - `ProjectileEntityLogicFactory`
- 이는 파일 배치 타협이며, 책임 경계 자체를 되돌린 것은 아니다.

현재 구현 기준으로 phase 공용 실행 모델은 `Gameplay_Model`에 둔다.

- `Intent`, `ActionGroup`, `TickPhase`는 특정 feature가 아니라 공용 실행 모델 소유다.
- `MoveIntent`, `AttackIntent` 같은 concrete intent만 각 feature 레이어에 둔다.
- `IntentComparer`, `ActionGroupComparer`는 feature 전용 비교기가 아니라 공용 정렬 계약이다.
- 다만 `intentId` 발급 직전의 pre-ID ordering은 phase별 규칙을 따른다.
  - `Movement`: `sourceId asc`
  - `Attack`: 정규화 후 `sourceId asc -> inputKind asc -> localSequence asc`
- `MoveAction`, `DamageAction`, `DestroyAction`, `SpawnAction`, `StateChangeAction`도 `ActionGroup`과 함께 공용 모델로 관리한다.

### 4-2. Dynamic EntityLogic 조립 원칙

`TickPipeline`은 phase orchestration만 책임지고, dynamic `IEntityLogic` materialization 정책은 별도 provider 계층이 소유한다.

- `TickPipeline`은 `ISnapshotEntityLogicProvider`를 생성하지 않는다.
- `TickPipeline`은 `ProjectileEntityLogicFactory` 같은 concrete factory를 참조하지 않는다.
- snapshot 기반 dynamic logic 복구는 `ISnapshotEntityLogicProvider`가 담당한다.
- entity type별 concrete materialization은 `IEntityLogicFactory`가 담당한다.
- static `IEntityLogic`와 dynamic `IEntityLogic`의 phase ownership 충돌 판단은 provider가 담당한다.

권장 책임 분리:

- `GameplayCompositionRoot`
  - 런타임 기본 의존성 그래프 조립
  - `ISnapshotEntityLogicProvider` 생성
  - `TickPipeline` 생성
- `GameplayBootstrapper`
  - Unity 시작점
  - world/config/input source 준비
  - composition root 호출
  - runner 시작
- `TickPipeline`
  - tick 실행
  - phase 호출
  - result/trace/hash 생성
- `ISnapshotEntityLogicProvider`
  - snapshot을 읽고 이번 tick의 dynamic entity logic 집합 구성
- `IEntityLogicFactory`
  - 특정 `EntityState`를 concrete `IEntityLogic`로 변환

핵심 규칙:

- `TickPipeline` 기본 생성 경로 안에서 provider를 조립하지 않는다.
- provider 기본 조립은 `GameplayEntityLogicProviderFactory.CreateDefault()` 또는 상위 composition root에서만 수행한다.
- 새 autonomous entity type 추가 시 `TickPipeline` 수정 없이 factory 등록만으로 연결 가능해야 한다.

현재 구현 상태:

- `TickPipeline`은 provider를 필수 생성자 인자로 받고, 기본 provider 조립을 내부에서 하지 않는다.
- `GameplayCompositionRoot`와 `GameplayEntityLogicProviderFactory`를 통해 기본 조립 경로가 열려 있다.
- `SnapshotEntityLogicProvider`와 composition root 관련 타입은 아직 별도 파일로 완전히 분리되지 않았고, current-state에서는 `TickPipeline.cs`에 co-locate되어 있다.
- 따라서 책임 분리는 반영됐지만, 파일 배치와 Unity 시작점 wiring은 target-state까지 아직 남아 있다.

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

중요 원칙:

- `TickPipeline`은 실행 순서만 소유한다.
- `TickPipeline`은 dynamic entity materialization 정책을 소유하지 않는다.
- `TickPipeline`은 provider를 필수 생성자 인자로 받거나, composition root가 조립한 provider를 전달받는다.
- 내부 `CreateDefaultEntityLogicProvider()` 같은 기본 조립 메서드는 두지 않는다.

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

### 6-3. Composition Root / Bootstrapper 상세 계획

이 단계는 projectile authority 복구 이후 남아 있던 `OCP`, `SRP`, `DIP` 정리를 위한 구조 단계다.

현재 상태: 부분 완료

- 완료
  - `TickPipeline` 생성자는 `ISnapshotEntityLogicProvider`를 필수 인자로 받는다.
  - 기본 provider 조립은 `GameplayEntityLogicProviderFactory.CreateDefault()`로 이동했다.
  - `ProjectileEntityLogicFactory`는 `Gameplay_Entities` 계층에 있다.
  - `TickPipeline` 클래스는 provider 결과만 소비하고, 내부 default composition 메서드를 갖지 않는다.
- 부분 완료
  - `GameplayCompositionRoot`, `GameplayBootstrapper`, `GameplayEntityLogicProviderFactory`, `SnapshotEntityLogicProvider`는 존재하지만 generated `.csproj` 제약 때문에 아직 `TickPipeline.cs`에 co-locate되어 있다.
  - `GameplayBootstrapper`는 조립 helper 역할은 수행하지만, 실제 Unity 시작점/runner wiring까지는 아직 맡지 않는다.
- 미완료
  - `SnapshotEntityLogicProvider`를 별도 `Gameplay_Entities` 파일/계층으로 이동
  - composition root/bootstrapper를 실제 runtime entrypoint에 연결

도입 대상 타입:

- `GameplayCompositionRoot`
- `GameplayBootstrapper`
- `GameplayEntityLogicProviderFactory`
- `ISnapshotEntityLogicProvider`
- `IEntityLogicFactory`
- `IEntityLogicSourceBinding`

구현 목표:

1. `TickPipeline` 생성자는 `ISnapshotEntityLogicProvider`를 필수 인자로 받는다.
2. 기본 provider 조립은 `GameplayEntityLogicProviderFactory.CreateDefault()`로 이동한다.
3. Unity 진입점은 `GameplayBootstrapper`가 맡고, 여기서 composition root를 호출한다.
4. `SnapshotEntityLogicProvider`는 `Gameplay_Entities` 계층으로 이동한다.
5. `ProjectileEntityLogicFactory`도 `Gameplay_Entities` 계층으로 이동한다.
6. `TickPipeline`은 더 이상 projectile/factory/provider concrete type을 모른다.

초기 권장 흐름:

1. `GameplayBootstrapper`가 world/config/static logic를 준비한다.
2. `GameplayCompositionRoot`가 provider를 조립한다.
3. `GameplayCompositionRoot`가 `TickPipeline`을 생성한다.
4. bootstrapper가 runner 또는 호출자에게 pipeline을 넘긴다.

예시 책임:

- `GameplayEntityLogicProviderFactory.CreateDefault()`
  - `ProjectileEntityLogicFactory`
  - 이후 `TurretEntityLogicFactory`, `TrapEntityLogicFactory` 등 확장 지점
- `SnapshotEntityLogicProvider`
  - snapshot ordered enumeration
  - static/dynamic logic merge
  - phase ownership conflict 제거
- `TickPipeline`
  - `provider.Build(snapshot, staticEntityLogics)` 결과 소비만 수행

## 7. 단계별 구현 계획

| 단계 | 범위 | 현재 상태 | 비고 |
| --- | --- | --- | --- |
| 단계 0 | 구조 뼈대 고정 | 완료 | 기본 phase 실행 경로와 테스트 프로젝트가 존재한다. |
| 단계 1 | 도메인 골격 구현 | 완료 | `WorldState`, `WorldSnapshot`, `Intent`, `ActionGroup`, allocator 뼈대가 고정됐다. |
| 단계 1.5 | EntityLogic 조립 책임 분리 | 부분 완료 | provider 필수 주입과 composition root는 반영됐고, 파일/entrypoint 정리는 남아 있다. |
| 단계 2 | 최소 Movement 수직 슬라이스 | 완료 | push/edge reservation까지 포함해 최소 범위를 넘어 확장됐다. |
| 단계 3 | 최소 Attack 수직 슬라이스 | 완료 | attack, projectile spawn, delayed-event 경계까지 검증된다. |
| 단계 4 | Cleanup 구현 | 완료 | remove/state timer/state transition 규칙이 테스트로 고정됐다. |
| 단계 5 | TickResult, Trace, Replay | 완료 | trace/hash/replay/fuzz 경로가 존재한다. |
| 단계 6 | reservation과 확장 기능 | 완료 | `ImpactReservation`, projectile movement, spawn, pushchain, edge reservation, on-hit 금지 경계가 반영됐다. |

## 7-1. 단계 0: 구조 뼈대 고정

현재 상태: 완료

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

현재 상태: 완료

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

## 7-3. 단계 1.5: EntityLogic 조립 책임 분리

현재 상태: 부분 완료

산출물:

- `IEntityLogicSourceBinding`
- `IEntityLogicFactory`
- `ISnapshotEntityLogicProvider`
- `SnapshotEntityLogicProvider`
- `ProjectileEntityLogicFactory`
- `GameplayEntityLogicProviderFactory`
- `GameplayCompositionRoot`
- `GameplayBootstrapper`

구현 단계:

1. `TickPipeline`에서 provider 기본 조립 메서드를 제거한다.
2. `TickPipeline` 생성자에서 provider를 필수 인자로 승격한다.
3. 현재 provider/factory/concrete projectile 의존을 `Gameplay_Entities` 계층으로 이동한다.
4. 기본 런타임 조립은 `GameplayEntityLogicProviderFactory.CreateDefault()`에 모은다.
5. 상위 시작 지점에서 `GameplayCompositionRoot`를 통해 pipeline을 조립한다.
6. 기존 테스트는 fake provider 주입 방식으로 유지한다.

완료 기준:

- `TickPipeline`은 `ISnapshotEntityLogicProvider` 외 concrete dynamic logic 타입을 직접 참조하지 않는다.
- `TickPipeline` 내부에 기본 provider 조립 메서드가 없다.
- 새 factory 추가만으로 기본 runtime provider를 확장할 수 있다.
- fresh pipeline + pre-existing projectile 복구 시나리오가 유지된다.
- 구조 테스트가 provider 주입 경로와 concrete 의존 제거를 검증한다.

남은 작업:

- `SnapshotEntityLogicProvider`와 composition root 관련 타입을 별도 파일/계층으로 이동
- `GameplayBootstrapper`를 실제 Unity 시작 지점/runner wiring에 연결

## 7-4. 단계 2: 최소 Movement 수직 슬라이스

현재 상태: 완료

지원 범위:

- `MoveIntent`
- 1칸 이동
- blocked면 실패
- 동일 목적지 충돌 시 하나만 선택
- `Stop` fallback 허용 여부는 문서로 고정

구현 순서:

1. `MovementIntentCollector`
2. raw intent dump
3. `sourceId asc` 결정론적 정렬
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

## 7-5. 단계 3: 최소 Attack 수직 슬라이스

현재 상태: 완료

지원 범위:

- `AttackIntent`
- 인접 대상 단일 데미지
- `StateChange -> Damage -> DestroyMark`만 구현
- Spawn 없음
- reservation 없음

구현 순서:

1. `AttackIntentCollector`
2. raw attack intent dump
3. `NormalizeAttackInputs`
4. `sourceId asc -> inputKind asc -> localSequence asc` 결정론적 정렬
5. post-sort `intentId` 발급
6. `AttackExpander`
7. `ActionGroupComparer`
8. post-sort `groupId` 발급
9. `AttackResolver`
10. `AttackCommitter`

완료 기준:

- 살아 있는 엔티티만 Attack Intent 생성
- 공격 중 사망해도 Cleanup 전까지 occupancy 유지
- 같은 Tick 내 이미 생성된 attack candidate는 유지

## 7-6. 단계 4: Cleanup 구현

현재 상태: 완료

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

## 7-7. 단계 5: TickResult, Trace, Replay

현재 상태: 완료

산출물:

- `TickResultBuilder`
- `TickTraceBuilder`
- `DeterminismHashBuilder`
- replay harness

완료 기준:

- 매 Tick trace 파일 또는 문자열 생성
- 매 Tick 짧은 hash 생성
- 동일 입력 replay 시 동일 hash 보장

## 7-8. 단계 6: reservation과 확장 기능

현재 상태: 완료

현재 구현 메모:

- `ImpactReservation`, attack input 정규화, projectile movement, spawn은 이미 연결돼 있다.
- `PushChain`과 `edge reservation`은 테스트로 고정돼 있다.
- generic `on-hit` 시스템은 열지 않았고, 대신 same-tick 재진입 금지 경계를 테스트와 delayed-event 방향으로 고정했다.

구현 순서 기록:

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

## 7-9. Stage6 상세 설계 기록

현재 상태: 완료

이 섹션은 Stage6를 구현하면서 고정한 상세 설계 기록이다.
`PushChain`, `edge reservation`, `on-hit 확장 검토`는 새 Phase를 추가하지 않고, 기존 `Movement -> Attack -> Cleanup` 내부에만 합류한다.

현재 코드 기준 전제:

- `Projectile movement`, `ImpactReservation`, `Spawn`, `Attack input 정규화`는 이미 연결되어 있다.
- `MovementResolver`는 `destination reservation`, `edge reservation`, `shared moved entity` 충돌을 함께 본다.
- `MoveAction`은 `Source`, `Destination`, `Facing`을 함께 가진다.
- `Attack`은 explicit `Attack` / `FireProjectile` / synthetic `ImpactReservation`로 구분된다.

### 7-9-1. PushChain 설계

목표:

- `PushChain`을 Attack이 아니라 Movement 확장으로 연다.
- 밀기 성공/실패는 `S0` 기준으로만 판정한다.
- Commit에서는 선택된 체인만 이동시키고, 중간 damage나 remove는 하지 않는다.

초기 정책:

- `PushChain`은 `Unit`만 대상으로 한다.
- `Projectile`은 push 대상이 아니다.
- 체인 끝이 비어 있어야만 성공한다.
- 체인 중간에 `Unit`이 아닌 blocker가 있거나, push 불가 상태가 있으면 전체 실패다.
- 부분 밀기는 금지한다.
- 같은 체인 멤버를 공유하는 후보끼리는 동시에 선택될 수 없다.

현재 구조에 맞춘 최소 표현:

- 새 raw intent 타입을 추가하지 않는다.
- 기존 `RawMovementIntent` / `MoveIntent`를 그대로 사용한다.
- `MovementExpander`가 `destination`에 `Unit`이 있는 경우 `Move` 대신 `PushChain` 후보를 생성한다.
- 성공한 `PushChain` 후보는 하나의 `ActionGroup` 안에 여러 `MoveAction`을 담는다.
- 이때 `MoveAction` 목록의 순서는 `체인 끝 -> 체인 시작`으로 고정한다.

반영된 모델 보강:

- `ActionGroupKind.PushChain` 추가
- `MoveAction`에 `Source` 좌표를 추가한다.
  이유:
  - Resolver가 `edge reservation`을 계산할 때 출발/도착 edge가 필요하다.
  - Committer가 현재 위치를 재질의하지 않고, Expander가 결정한 경로를 그대로 적용할 수 있어야 한다.

Expander 알고리즘:

1. source의 이동 delta를 계산한다.
2. `destination`에 `Unit`이 없으면 기존 `Move` 후보 생성 규칙을 따른다.
3. `destination`에 `Unit`이 있으면 같은 delta 방향으로 연속 점유 체인을 걷는다.
4. 첫 빈칸을 찾으면 `PushChain` 성공 후보를 하나 만든다.
5. 빈칸을 찾지 못하거나, 체인 중간에 push 불가 대상을 만나면 실패로 끝낸다.
6. 실패 시 현재 구조를 유지하기 위해 초기 버전은 `Stop` 후보를 만들지 않고 `rejectedReasons`만 남긴다.

체인 멤버별 이동 규칙:

- 맨 끝 엔티티가 빈칸으로 이동
- 그 앞 엔티티가 방금 비워질 칸으로 이동
- 마지막으로 source가 원래 destination으로 이동
- source의 facing은 이동 방향으로 갱신
- 밀린 엔티티의 facing은 초기 버전에서는 유지한다

Resolver 규칙:

- 같은 `intentId`에서는 최대 하나의 후보만 선택
- `PushChain` 후보는 아래 두 종류의 충돌을 함께 검사
  - 최종 `destination reservation` 충돌
  - 체인 멤버 `entityId` 공유 충돌
- 이미 선택된 후보와 체인 멤버를 공유하면 later candidate를 reject한다.
- reject reason은 `SharedPushChainMember`로 고정한다.

Commit 규칙:

- `MovementCommitter`는 `PushChain` group의 `MoveAction`을 리스트 순서대로 적용한다.
- 이동 이벤트는 기존 `MoveCommitted` 포맷을 재사용한다.
- `PushChain` 자체는 `PhaseTransientBuffer`를 사용하지 않는다.
- Push 결과로 damage, destroy, spawn을 직접 만들지 않는다.

테스트 우선순위:

- `Movement_PushChain_SucceedsWhenChainEndsAtEmptyCell`
- `Movement_PushChain_FailsWhenChainContainsUnpushableBlocker`
- `Movement_PushChain_RejectsLaterCandidateThatSharesChainMember`
- `Movement_PushChain_CommitsFromTailToHeadDeterministically`
- `Replay_PushChainScenario_ProducesSameHashTraceAndEventLog`

### 7-9-2. edge reservation 설계

목표:

- `destination reservation`만으로 표현되지 않는 경로 충돌을 Resolver에서 명시적으로 다룬다.
- `edge reservation`은 월드 상태가 아니라 MovementResolver 내부의 Tick-local 자료다.
- 다음 Tick으로 이월하지 않는다.

초기 목적:

- `PushChain` 다중 이동 경로 충돌 잠금
- 향후 고속 projectile / slide / multi-step path 확장을 위한 준비

핵심 원칙:

- `edge reservation`은 `MovementResolver`의 선택 충돌 판정에만 사용한다.
- `WorldState`, `WorldSnapshot`, `PhaseTransientBuffer`에는 저장하지 않는다.
- 의미론은 여전히 `S0` 기준이며, Commit 후 world를 다시 읽어 판단하지 않는다.

권장 데이터:

```csharp
internal readonly struct EdgeReservation
{
    public int entityId;
    public Vector2Int from;
    public Vector2Int to;
    public int groupId;
}
```

초기 충돌 규칙:

- 동일한 undirected edge를 공유하는 later candidate는 reject
- 같은 후보 내부의 edge들은 이미 Expander가 정합성을 보장한다고 가정
- 기존 `destination reservation` 충돌은 그대로 유지
- `edge reservation`은 `destination reservation`을 대체하지 않고 보완한다

Resolver 알고리즘 변경:

1. 후보의 모든 `MoveAction`에서 `destination` 예약을 계산
2. 후보의 모든 `MoveAction`에서 `edge reservation`을 계산
3. 이미 선택된 후보와 `destination` 충돌이 있으면 reject
4. 이미 선택된 후보와 `edge` 충돌이 있으면 reject
5. 충돌이 없으면 후보 선택 후 두 reservation을 모두 기록

초기 범위에서 명시적으로 다루는 것:

- `PushChain` vs `PushChain` 경로 겹침
- 이후 고속 경로 확장 시 head-on crossing

초기 범위에서 일부러 열지 않는 것:

- curved path
- speed 2 이상 projectile 세분 경로
- diagonal edge

테스트 우선순위:

- `Movement_EdgeReservation_RejectsLaterCandidateThatSharesUndirectedEdge`
- `Movement_EdgeReservation_StillRejectsDestinationConflict`
- `Movement_EdgeReservation_DoesNotPersistAcrossTicks`
- `Replay_EdgeReservationScenario_ProducesSameHashTraceAndEventLog`

### 7-9-3. on-hit 확장 검토

결론부터 적는다.

- Stage6에서는 generic `on-hit` 연쇄를 바로 열지 않는다.
- 문서 원칙상 Commit 중간 새 Intent 생성은 계속 금지한다.
- 따라서 `반격`, `처치 즉시 추가 공격`, `즉시 teleport`, `추가 target 선택`은 Stage6 범위 밖이다.

현재 모델에서 허용되는 same-tick 효과:

- 기존 입력 또는 `ImpactReservation`에서 Expander가 미리 계산할 수 있는 추가 action
- 예:
  - projectile self-destroy
  - 고정 `SpawnAction`
  - 고정 `StateChangeAction`
  - 고정 `DamageAction`

현재 모델에서 금지되는 same-tick 효과:

- Commit 결과를 읽고 그 자리에서 새 intent를 만드는 것
- kill 여부를 본 뒤 다른 target을 새로 고르는 것
- Attack 중간에 Movement를 다시 여는 것
- 피격 즉시 반격을 같은 Tick에 실행하는 것

허용/금지 기준:

- Expander 시점에 입력과 snapshot만으로 완전히 확정 가능하면 same-tick action으로 허용 가능
- `finalHp`, `DestroyMark`, Cleanup 결과, 다른 후보의 commit 결과를 읽어야 하면 same-tick 금지

필요 시 다음 단계의 권장 방향:

- `on-hit`은 `next-tick delayed event`로만 모델링한다.
- 이를 위해 `PhaseTransientBuffer`와 별도의 영속 queue가 필요하다.
- 이 queue는 `WorldState`가 아니라 loop 계층이 소유한다.
- Committer는 새 intent를 만들지 않고 `tick + 1`용 effect record만 enqueue한다.
- 다음 Tick 시작 시 해당 effect를 system-generated input으로 변환해 정규화 체인에 합류시킨다.

권장 분류:

1. 즉시 action으로 충분한 효과
2. next-tick delayed event가 필요한 효과
3. 현재 설계 바깥이라 금지할 효과

예시:

- projectile impact 후 self-destroy: 1
- hit 시 고정 debuff state 적용: 1
- kill 시 새 target으로 튕기는 chain attack: 2
- 피격 즉시 반격: 2
- on-hit 즉시 teleport: 3

Stage6에서 실제로 할 일:

- generic on-hit 시스템은 구현하지 않는다.
- 대신 허용/금지 경계를 테스트와 문서로 먼저 잠근다.

테스트 우선순위:

- `Attack_OnHit_DoesNotCreateSameTickNewIntent`
- `Attack_OnHit_DoesNotReenterMovementPhase`
- `Attack_ImpactReservation_CanStillExpandToFixedSameTickActions`

### 7-9-4. Stage6 실제 착수 순서 기록

문서 순서와 현재 코드 상태를 함께 고려한 실제 순서는 아래가 맞다.

1. `PushChain` 성공/실패와 shared-chain conflict를 먼저 연다.
2. 그 다음 `MoveAction.Source`와 resolver 내부 `edge reservation`을 추가한다.
3. `edge reservation`이 기존 `Move`, `ProjectileImpact`, `PushChain` 결정론을 깨지 않는지 replay로 고정한다.
4. `on-hit`은 구현보다 금지 경계와 delayed-event 방향만 문서화한다.

이 순서를 지켜야 하는 이유:

- `PushChain`이 먼저 열려야 `edge reservation`의 실제 적용 대상을 갖게 된다.
- `edge reservation`을 먼저 열면 현재 one-step 구조에서는 실효성이 약하고 설계만 커진다.
- `on-hit`을 먼저 열면 Commit 중간 intent 생성 금지 원칙을 깨기 쉽다.

## 8. 상세 Phase 설계

### 8-1. Movement Phase

초기 메서드 체인:

```text
CollectMovementIntents
-> SortMovementInputsBySource
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
-> NormalizeAttackInputs
-> SortAttackInputs
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
- `AttackCommitter`가 재사용된 출력 버퍼도 매 호출마다 완전히 재구성하는가

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
- `TickPipeline`이 provider/factory concrete 조립 책임까지 다시 떠안고 있는가
- 기본 runtime provider 조립이 composition root/bootstrapper 밖으로 새고 있지는 않은가
- 새 autonomous entity type 추가 시 pipeline 수정이 필요한 구조인가

## 12. 각 단계의 완료 정의와 현재 상태

### 12-1. 구조 단계 완료

현재 상태: 완료

완료 조건:

- `RunTick`이 빈 Phase라도 끝까지 돈다.
- `TickPipeline` 외 우회 실행 경로가 없다.
- 테스트 프로젝트가 생성되어 있다.

### 12-2. 최소 전투 슬라이스 완료

현재 상태: 완료

완료 조건:

- 이동과 공격, Cleanup이 한 Tick 흐름으로 연결된다.
- `hp <= 0` 제거 시점이 Cleanup으로 고정된다.
- trace와 hash가 남는다.

### 12-3. 확장 준비 완료

현재 상태: 완료

완료 조건:

- replay test가 통과한다.
- scenario test가 핵심 규칙을 덮는다.
- reservation 추가를 위한 `PhaseTransientBuffer` 자리가 이미 존재한다.

### 12-4. 리뷰 반영 완료

현재 상태: 완료

완료 조건:

- `Movement`와 `Attack`의 `intentId` 발급 문서가 현재 phase별 정렬 규칙과 충돌 없이 읽힌다.
- `AttackCommitter`는 매 호출마다 `commitEvents`, `delayedAttackEnqueueEvents`를 모두 초기화한 뒤 다시 기록한다.
- 회귀 테스트가 재사용된 출력 버퍼에서도 동일한 attack event 결과를 보장한다.

### 12-5. Composition Root 단계 완료

현재 상태: 부분 완료

완료 조건:

- `TickPipeline`은 orchestration-only 객체로 읽힌다.
- `TickPipeline` 생성자는 provider를 외부에서 전달받는다.
- 기본 provider 조립은 `GameplayEntityLogicProviderFactory` 또는 `GameplayCompositionRoot`에만 존재한다.
- `SnapshotEntityLogicProvider`와 concrete entity logic factory는 `Gameplay_Entities` 계층에 존재한다.
- `OCP` 관점에서 새 autonomous entity type 추가 시 pipeline 수정이 필요 없다.
- replay/scenario/structure 테스트가 모두 통과한다.

current-state 메모:

- 현재 구현은 `TickPipeline` 클래스 기준으로 orchestration-only와 provider 필수 주입 조건을 만족한다.
- `GameplayEntityLogicProviderFactory`, `GameplayCompositionRoot`, `GameplayBootstrapper`, `SnapshotEntityLogicProvider`는 책임상 분리됐지만, generated `.csproj` 제약으로 아직 `TickPipeline.cs`에 co-locate되어 있다.
- `ProjectileEntityLogicFactory`는 이미 `Gameplay_Entities` 계층에 있다.
- 따라서 composition root 단계는 책임 분리 측면에서는 반영됐고, 최종 파일/entrypoint 정리만 남아 있다.

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
10. projectile authority 복구 이후 provider/factory/provider-composition을 `Gameplay_Entities + CompositionRoot` 구조로 정리
11. `TickPipeline`에서 기본 provider 조립 제거
12. bootstrapper/composition root 기반 런타임 조립과 구조 테스트 보강

## 14. 한 줄 구현 원칙

가장 중요한 구현 원칙은 아래 한 줄이다.

> 먼저 뼈대를 잠그고, 최소 수직 슬라이스를 끝까지 관통시킨 뒤, 로그와 리플레이 테스트로 결정론을 고정한 다음에만 복잡도를 연다.

## 15. 잔여 작업 상세 설계

이 섹션은 current-state 조사 결과를 바탕으로, 현재 문서에서 "남은 작업"으로 남아 있는 항목을 실제 구현 가능한 단위로 다시 정리한 것이다.

핵심 전제:

- 결정론 코어 자체는 이미 반영돼 있다.
- 남은 작업의 본질은 "새 전투 규칙 추가"가 아니라 "구조 정리 + 런타임 진입 경로 완성"이다.
- 따라서 이번 단계는 `TickPipeline`의 phase 규칙을 바꾸지 않고, orchestration 외부 조립과 runtime driving 경로만 닫는다.

### 15-1. 조사 결과 요약

현재 코드 기준으로 남은 항목은 세 묶음이다.

1. composition root 정리
   - `SnapshotEntityLogicProvider`, `GameplayEntityLogicProviderFactory`, `GameplayBootstrapper`, `GameplayCompositionRoot`가 책임상 분리됐지만 아직 `TickPipeline.cs`에 co-locate되어 있다.
2. runtime tick driving 경로 부재
   - `TickRunner`, `TickInputBuffer`가 아직 없어서 테스트 밖에서 공용 tick 실행 경로가 없다.
   - 현재 테스트와 replay harness는 모두 `GameplayCompositionRoot.CreateTickPipeline(...)`를 직접 호출한다.
3. concrete write-context 명시화 미완료
   - `IWorldWriteContext`는 존재하지만 concrete `WorldStateWriteContext`는 아직 `WorldState` 내부 private `WriteContext`로만 존재한다.
   - 다만 이것은 누락이라기보다, "concrete write path를 `WorldState` 내부에 숨긴다"는 초기 의도를 반영한 current-state다.
   - 따라서 남은 작업의 본질은 "concrete type이 없다"가 아니라, "숨김 구조를 유지한 채 target-state 파일 분리와 구조 명시화를 어떻게 달성할 것인가"다.

추가 current-state 메모:

- `TickInput`은 현재 `tickIndex`만 가진 최소 구조다.
- `ProjectileLogic`와 `ProjectileEntityLogicFactory`는 이미 존재하지만, `PlayerLogic` / `EnemyLogic` / `TerrainData`는 아직 없다.
- `WorldState.CreateWriteContext()`는 `internal`이며, production runtime 경로에서는 `TickPipeline`이 사용하고, 테스트는 focused verification을 위해 직접 사용할 수 있다.
- 따라서 "결정론 전투 코어"는 완료됐지만 "실제 runtime 조립 경로"와 "target-state 파일 분리"는 아직 닫히지 않았다.

이번 설계는 이 중에서 아래 항목을 이번 정리 범위로 본다.

- `SnapshotEntityLogicProvider` 분리
- `GameplayEntityLogicProviderFactory` 분리
- `GameplayBootstrapper` / `GameplayCompositionRoot` 분리
- `WorldStateWriteContext` 명시화
- `TickInputBuffer` 도입
- `TickRunner` 도입

이번 정리 범위에서 의도적으로 미루는 항목:

- `TerrainData`
- `PlayerLogic`
- `EnemyLogic`

이 세 항목은 "조립층 마감"보다 "실제 게임 룰/입력 스키마 정의"에 더 강하게 의존하므로, placeholder를 억지로 추가하는 것보다 deferred normalization으로 두는 편이 낫다.

### 15-2. 이번 단계의 완료 정의

이번 설계가 구현되면 아래 조건을 만족해야 한다.

- `TickPipeline.cs`에는 `TickPipeline`만 남는다.
- provider/factory concrete type은 `Gameplay_Entities` 계층으로 이동한다.
- 조립 public API는 `GameplayCompositionRoot`와 `GameplayBootstrapper`에만 남는다.
- 표준 runtime 실행 경로는 `TickInputBuffer -> TickRunner -> TickPipeline` 한 줄로 읽힌다.
- 기존 테스트가 의존하는 `GameplayCompositionRoot.CreateTickPipeline(...)` API는 유지된다.
- Committer 외 계층이 `WorldState`를 직접 수정할 수 없는 구조는 그대로 유지된다.
- current-state에서 의도적으로 숨겨 둔 concrete write path의 은닉 성질이 target-state에서도 약화되지 않는다.

### 15-3. 책임 재정의

이번 단계에서 각 타입의 책임은 아래처럼 고정한다.

- `TickPipeline`
  - phase orchestration
  - snapshot 재생성
  - post-sort ID 발급
  - trace/hash/result 생성
  - provider 결과 소비
- `SnapshotEntityLogicProvider`
  - snapshot ordered enumeration
  - static/dynamic logic merge
  - phase ownership conflict 제거
- `GameplayEntityLogicProviderFactory`
  - 기본 runtime dynamic factory 집합 구성
- `GameplayBootstrapper`
  - world/static logic/input buffer를 받아 pipeline 또는 runner 조립
  - plain C# composition object로 유지
- `GameplayCompositionRoot`
  - default provider/bootstrapper factory
  - assembly-level convenience entry API 유지
- `TickInputBuffer`
  - tick-indexed input staging
  - sampling boundary 고정
- `TickRunner`
  - monotonic tick index 관리
  - buffer에서 입력 소비
  - `TickPipeline.RunTick` 호출

중요 해석:

- 이번 설계에서는 `GameplayBootstrapper`를 `MonoBehaviour`로 만들지 않는다.
- 이유는 현재 구조 테스트와 replay/scenario 테스트가 plain object bootstrapper/factory 계약을 전제하고 있기 때문이다.
- 실제 Unity scene host가 필요하면 이후 상위 host/view 계층이 `GameplayBootstrapper`나 `TickRunner`를 호출하는 thin adapter를 둔다.
- 즉, deterministic runtime asmdef는 계속 순수 시뮬레이션 조립 계층으로 유지한다.

### 15-4. 목표 파일 분리안

이번 단계 완료 후 권장 파일 배치는 아래와 같다.

```text
Assets/_Features/Gameplay/
  Gameplay_Loop/
    Runtime/
      TickPipeline.cs
      GameplayCompositionRoot.cs
      GameplayBootstrapper.cs
      TickRunner.cs
      TickInputBuffer.cs
  Gameplay_Entities/
    Runtime/
      IEntityLogic.cs
      SnapshotEntityLogicProvider.cs
      GameplayEntityLogicProviderFactory.cs
      ProjectileLogic.cs
  Gameplay_BoardState/
    Runtime/
      WorldState.cs
      WorldStateWriteContext.cs
      IWorldWriteContext.cs
```

적용 원칙:

- `TickPipeline.cs`에서는 nested/co-located type을 제거한다.
- `ProjectileEntityLogicFactory`는 기존처럼 `ProjectileLogic.cs`에 co-locate를 유지해도 된다.
  - 이 파일은 이미 `Gameplay_Entities` 계층에 있기 때문이다.
- `WorldStateWriteContext`는 별도 파일로 추출하되 capability 경계는 `IWorldWriteContext`로 유지한다.

### 15-5. `WorldStateWriteContext` 구체 설계

조사 결과 기준 current-state 해석:

- 현재 nested private `WriteContext`는 accidental leftover가 아니다.
- 초기 stage0 구현부터 `WorldState`의 concrete write path를 내부에 숨기는 방향으로 들어갔고, 이후 reflection 구조 테스트로 고정됐다.
- 즉, 현재 구조의 핵심 의도는 "쓰기 capability는 interface로만 보이고, concrete mutation path는 `WorldState` 내부에 감춘다"는 점이다.

현재 nested private `WriteContext`를 top-level concrete type으로 꺼내려면, `WorldState` private helper에 직접 접근할 수 없다는 문제가 생긴다.

따라서 이 항목의 설계 목표는 단순 추출이 아니라 아래 두 조건을 동시에 만족하는 것이다.

1. target-state 파일 분리와 concrete type 명시화
2. current-state가 의도적으로 확보한 concrete write path 은닉 유지

#### 15-5-1. current-state의 실제 보호 수준

현재 구조가 막고 있는 것:

- `WorldState` public API를 통한 직접 mutation
- 외부 코드가 concrete write context 타입에 정적으로 의존하는 것
- same-assembly 코드가 `AddNewEntity`, `ClearOccupancy`, `RemoveEntity`, `SetOccupancy`, `TryGetEntity`, `UpdateEntity`를 직접 호출하는 것

현재 구조가 완전히 막지는 않는 것:

- `internal WorldState.CreateWriteContext()` 경로의 same-assembly 사용
- `InternalsVisibleTo("Game.Feature.Gameplay.Tests")`를 통한 테스트의 직접 write-context 생성

이 점은 설계 문서에 명시적으로 반영해야 한다.

- production runtime 규칙은 "표준 경로에서 `TickPipeline`이 write context를 생성한다"로 읽어야 한다.
- 테스트는 phase 단위 검증을 위해 internal factory를 직접 호출할 수 있다.
- 따라서 "절대적으로 `TickPipeline`만 생성 가능"을 구조 invariant로 삼으면 current-state와 맞지 않는다.

#### 15-5-2. 설계 선택지

선택지 A. current-state 유지

- `WriteContext`를 nested private로 그대로 둔다.
- 장점:
  - 은닉 강도가 가장 높다.
  - private helper를 그대로 유지할 수 있다.
  - 구현 리스크가 가장 낮다.
- 단점:
  - target-state의 `WorldStateWriteContext.cs`와 맞지 않는다.
  - concrete write path를 문서/구조 관점에서 명시적으로 설명하기 어렵다.
  - file split 기준에서는 남은 작업이 계속 남는다.

선택지 B. top-level `WorldStateWriteContext` + `internal` mutation helper 공개

- `WorldState` helper를 `internal`로 열고 top-level concrete type이 직접 호출한다.
- 장점:
  - 구현이 단순하다.
  - 파일 분리가 쉽다.
- 단점:
  - current-state가 의도적으로 막아 둔 same-assembly direct mutation 통로가 넓어진다.
  - "구조적으로 숨긴다"는 초기 의도와 충돌한다.

선택지 C. top-level `WorldStateWriteContext` + explicit internal mutation port

- `WorldState`는 public surface를 비운 채 internal explicit interface로만 mutation helper를 노출한다.
- top-level concrete type은 이 port만 통해 mutation한다.
- 장점:
  - target-state 파일 분리 가능
  - concrete type 명시화 가능
  - same-assembly direct mutation 노출을 상대적으로 좁게 유지
  - current-state 은닉 의도와 target-state 명시화를 모두 수용
- 단점:
  - interface/adapter 계층이 하나 추가된다.
  - 구조 테스트 갱신이 필요하다.

선택:

- 이번 문서에서는 선택지 C를 채택한다.
- 이유는 current-state의 의도된 은닉을 유지하면서도 target-state의 explicit concrete type 요구를 가장 무리 없이 만족시키기 때문이다.

이번 단계에서는 아래 방식으로 해결한다.

1. `WorldState`가 internal mutation port를 explicit interface로 구현한다.
2. `WorldStateWriteContext`는 그 mutation port만 잡고 `IWorldWriteContext`를 구현한다.
3. public surface에는 mutation 메서드를 계속 노출하지 않는다.

권장 내부 인터페이스:

```csharp
internal interface IWorldStateMutationPort
{
    bool TryGetEntity(int entityId, out EntityState entity);
    void AddNewEntity(EntityState entity);
    void UpdateEntity(EntityState entity);
    void ClearOccupancy(EntityState entity);
    void SetOccupancy(EntityState entity);
    void RemoveEntityRecord(int entityId);
}
```

권장 `WorldState` 구조:

```csharp
public sealed class WorldState : IWorldStateMutationPort
{
    internal IWorldWriteContext CreateWriteContext()
    {
        return new WorldStateWriteContext((IWorldStateMutationPort)this);
    }

    bool IWorldStateMutationPort.TryGetEntity(int entityId, out EntityState entity) { ... }
    void IWorldStateMutationPort.AddNewEntity(EntityState entity) { ... }
    void IWorldStateMutationPort.UpdateEntity(EntityState entity) { ... }
    void IWorldStateMutationPort.ClearOccupancy(EntityState entity) { ... }
    void IWorldStateMutationPort.SetOccupancy(EntityState entity) { ... }
    void IWorldStateMutationPort.RemoveEntityRecord(int entityId) { ... }
}
```

권장 `WorldStateWriteContext` 구조:

```csharp
internal sealed class WorldStateWriteContext : IWorldWriteContext
{
    private readonly IWorldStateMutationPort _port;

    internal WorldStateWriteContext(IWorldStateMutationPort port)
    {
        _port = port;
    }

    public void MoveEntity(int entityId, Vector2Int destination) { ... }
    public void ApplyDamage(int entityId, int amount) { ... }
    public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer) { ... }
    public void MarkDestroy(int entityId) { ... }
    public void SpawnEntity(EntityState entity) { ... }
    public void RemoveEntity(int entityId) { ... }
    public void SetFacing(int entityId, Direction facing) { ... }
}
```

이 설계의 장점:

- `WorldStateWriteContext`를 top-level file/type로 분리할 수 있다.
- `WorldState` public API는 여전히 비어 있게 유지할 수 있다.
- Committer는 계속 `IWorldWriteContext`만 받는다.
- same-assembly 내부에서도 mutation helper를 임의 호출하기 어렵게 만든다.
- current-state nested/private 구조가 가지던 "concrete write path 은닉" 의도를 형태만 바꿔 유지할 수 있다.

구조 테스트 갱신 원칙:

- 기존 `WorldState_HidesConcreteWriteContext_And_PrivateMutationHelpers` 테스트는 더 이상 "top-level concrete type이 없어야 한다"를 보지 않는다.
- 대신 아래를 본다.
  - `WorldState` public mutation API 부재
  - `WorldStateWriteContext`가 internal type인가
  - `WorldState` mutation helper가 public/internal direct method로 새지 않았는가
  - production runtime path에서 `TickPipeline`이 write context를 생성하는가
  - 테스트는 focused verification을 위해 internal 경로를 호출할 수 있는가

권장 구조 테스트 이름 재편:

- `WorldState_DoesNotExposeDirectPublicMutationApi`
- `WorldStateWriteContext_IsInternalConcreteType`
- `WorldState_MutationHelpers_AreNotPublicApi`
- `TickPipeline_CreatesWorldWriteContext_OnProductionPath`

#### 15-5-3. 마이그레이션 단계

`WorldStateWriteContext` 추출은 아래처럼 두 단계로 나눈다.

1. 보호 의미 보존 단계
   - `IWorldStateMutationPort` 추가
   - `WorldState`가 explicit interface 구현
   - nested private `WriteContext`를 top-level `WorldStateWriteContext`로 이동
   - `CreateWriteContext()`는 계속 `internal`
2. 구조 잠금 단계
   - reflection 구조 테스트 갱신
   - same-assembly direct mutation 누수가 없는지 확인
   - `TickPipeline` runtime path와 테스트 path를 분리해 문서화

이 순서를 따르는 이유:

- 먼저 behavior-preserving extraction을 하고,
- 그 다음 테스트 invariant를 current-state 의도에 맞게 다시 잠그는 편이 안전하다.

### 15-6. `SnapshotEntityLogicProvider` / composition type 분리 설계

현재 `TickPipeline.cs` 안의 아래 타입을 분리한다.

- `SnapshotEntityLogicProvider`
- `GameplayEntityLogicProviderFactory`
- `GameplayBootstrapper`
- `GameplayCompositionRoot`

분리 후 책임 배치는 아래와 같다.

- `Gameplay_Entities/Runtime/SnapshotEntityLogicProvider.cs`
  - provider 구현
  - phase ownership conflict 규칙 유지
- `Gameplay_Entities/Runtime/GameplayEntityLogicProviderFactory.cs`
  - default dynamic factory 집합 생성
- `Gameplay_Loop/Runtime/GameplayBootstrapper.cs`
  - pipeline/runner 조립
- `Gameplay_Loop/Runtime/GameplayCompositionRoot.cs`
  - default bootstrapper factory
  - test/replay가 쓰는 assembly-level convenience API 유지

`TickPipeline`이 유지해야 할 규칙:

- 생성자는 계속 `ISnapshotEntityLogicProvider`를 필수 인자로 받는다.
- pipeline 내부에 default provider 조립 메서드를 두지 않는다.
- concrete factory 또는 concrete projectile logic type을 field로 보유하지 않는다.

### 15-7. `GameplayBootstrapper` 구체 API 설계

`GameplayBootstrapper`는 plain C# 조립 객체로 유지하고, 아래 API를 제공한다.

```csharp
public sealed class GameplayBootstrapper
{
    public GameplayBootstrapper(ISnapshotEntityLogicProvider entityLogicProvider);

    public TickPipeline CreateTickPipeline(WorldState worldState);
    public TickPipeline CreateTickPipeline(
        WorldState worldState,
        IEnumerable<IEntityLogic> entityLogics);

    public TickRunner CreateTickRunner(
        WorldState worldState,
        TickInputBuffer inputBuffer);

    public TickRunner CreateTickRunner(
        WorldState worldState,
        IEnumerable<IEntityLogic> entityLogics,
        TickInputBuffer inputBuffer,
        int startTickIndex = 1);
}
```

의도:

- 현재 테스트가 쓰는 `CreateTickPipeline(...)` 경로를 깨지 않는다.
- runtime 쪽은 `CreateTickRunner(...)`를 통해 표준 driving 경로를 쓸 수 있다.
- bootstrapper는 world/config/static logic 준비 책임만 갖고, phase 규칙이나 tick stepping은 소유하지 않는다.

### 15-8. `GameplayCompositionRoot` 구체 API 설계

`GameplayCompositionRoot`는 current public API를 유지하면서 runner 생성 API만 확장한다.

```csharp
public static class GameplayCompositionRoot
{
    public static GameplayBootstrapper CreateDefaultBootstrapper();

    public static TickPipeline CreateTickPipeline(WorldState worldState);
    public static TickPipeline CreateTickPipeline(
        WorldState worldState,
        IEnumerable<IEntityLogic> entityLogics);

    public static TickRunner CreateTickRunner(
        WorldState worldState,
        TickInputBuffer inputBuffer);

    public static TickRunner CreateTickRunner(
        WorldState worldState,
        IEnumerable<IEntityLogic> entityLogics,
        TickInputBuffer inputBuffer,
        int startTickIndex = 1);
}
```

구현 원칙:

- default provider 조립은 계속 `GameplayEntityLogicProviderFactory.CreateDefault()` 한 곳만 쓴다.
- `CreateTickRunner(...)`도 내부적으로는 `CreateDefaultBootstrapper()`를 통해 조립한다.
- replay/scenario/unit 테스트가 요구하는 direct pipeline path는 계속 남긴다.

### 15-9. `TickInputBuffer` 구체 설계

`TickInputBuffer`는 입력을 tick 경계에 귀속시키는 staging 저장소다.

현재 `TickInput`은 `tickIndex`만 가지므로 지금 당장은 기능이 단순해 보이지만, 향후 player command payload가 붙어도 구조를 바꾸지 않도록 지금 도입한다.

권장 API:

```csharp
public sealed class TickInputBuffer
{
    public void Record(in TickInput input);
    public bool TryConsume(int tickIndex, out TickInput input);
    public TickInput ConsumeOrDefault(int tickIndex);
    public bool HasBufferedInput(int tickIndex);
    public void ClearBefore(int tickIndex);
}
```

정책:

- `Record`는 `input.TickIndex <= 0`을 거부한다.
- 동일 `tickIndex`에 대한 중복 `Record`는 throw 한다.
  - silent overwrite를 허용하면 sampling source bug를 숨기기 쉽다.
- `ConsumeOrDefault(tickIndex)`는 버퍼에 입력이 있으면 그것을 꺼내고, 없으면 `new TickInput(tickIndex)`를 반환한다.
- 내부 저장소는 dictionary를 써도 되지만, 의미론은 key lookup만 사용한다.
  - 즉, iteration 순서를 의미론에 사용하지 않는다.

`TickInputBuffer`가 소유하지 않는 것:

- input sampling 타이밍
- frame time accumulation
- `TickPhase` 해석
- `WorldState` 접근

### 15-10. `TickRunner` 구체 설계

`TickRunner`는 monotonic tick sequence를 관리하는 얇은 driver다.

권장 API:

```csharp
public sealed class TickRunner
{
    public TickRunner(
        TickPipeline pipeline,
        TickInputBuffer inputBuffer,
        int startTickIndex = 1);

    public int NextTickIndex { get; }

    public TickResult RunNextTick();
    public TickResult RunTick(in TickInput input);
}
```

동작 규칙:

1. `RunNextTick()`는 `inputBuffer.ConsumeOrDefault(NextTickIndex)`를 호출한다.
2. 반환된 `TickInput`을 그대로 `TickPipeline.RunTick`에 전달한다.
3. tick 성공 후 `NextTickIndex`를 1 증가시킨다.
4. `RunTick(in TickInput input)` 직접 경로는 `input.TickIndex == NextTickIndex`일 때만 허용한다.
5. runner는 trace/hash/world query를 만들지 않는다.
   - 그 책임은 계속 `TickPipeline`에 남긴다.

runner가 plain class인 이유:

- replay harness와 테스트에서 manual stepping이 쉽다.
- Unity `Update` 종속이 없어 deterministic test에서 재사용하기 쉽다.
- 상위 host가 fixed-step accumulator를 어떤 방식으로 가지든 runner 코드는 유지된다.

### 15-11. 표준 runtime 흐름

이번 단계가 끝난 뒤 권장 흐름은 아래 한 줄이다.

1. 상위 host가 `TickInputBuffer.Record(...)`로 입력을 tick index에 귀속시킨다.
2. 상위 host가 `GameplayCompositionRoot.CreateTickRunner(...)` 또는 `GameplayBootstrapper.CreateTickRunner(...)`로 runner를 만든다.
3. 상위 host가 fixed-step 경계마다 `runner.RunNextTick()`을 호출한다.
4. 반환된 `TickResult`는 view/animation 쪽으로 전달된다.

이 흐름에서 중요한 점:

- View는 여전히 `TickResult`만 소비한다.
- host가 frame rate를 바꾸더라도 simulation은 `TickRunner`의 tick sequence만 따른다.
- `TickPipeline`은 runner 존재 여부를 모른다.

### 15-12. 테스트 보강 설계

이번 단계 구현과 함께 아래 테스트를 추가하거나 수정한다.

구조 테스트 유지/수정:

- 기존 `TickPipeline_DelegatesDynamicEntityMaterializationToProvider` 유지
- 기존 `GameplayCompositionRoot_ExposesDefaultPipelineAssemblyApi` 유지
- `WorldState_HidesConcreteWriteContext_And_PrivateMutationHelpers`는 아래 방향으로 갱신
  - `WorldState` public mutation API 없음
  - `WorldStateWriteContext`는 internal concrete type
  - `TickPipeline`만 `CreateWriteContext()`를 호출

새 unit test 권장:

- `TickInputBuffer_RecordRejectsDuplicateTick`
- `TickInputBuffer_ConsumeOrDefault_ReturnsRecordedInputOrDefaultTick`
- `TickRunner_RunNextTick_ConsumesBufferedInputAndAdvancesIndex`
- `TickRunner_RunTick_RejectsOutOfOrderTickIndex`
- `GameplayCompositionRoot_CreateTickRunner_UsesDefaultProvider`
- `GameplayBootstrapper_CreateTickRunner_PreservesPreExistingProjectileRecovery`

기존 scenario/replay test 유지 포인트:

- `GameplayCompositionRoot.CreateTickPipeline(...)` 시그니처는 그대로 유지한다.
- fresh pipeline + pre-existing projectile 복구 시나리오는 그대로 통과해야 한다.
- replay/fuzz는 runner 도입 여부와 무관하게 기존 pipeline direct path로도 유지 가능하다.

### 15-13. 구현 순서

실제 구현은 아래 순서가 가장 안전하다.

1. `SnapshotEntityLogicProvider` / `GameplayEntityLogicProviderFactory` / `GameplayBootstrapper` / `GameplayCompositionRoot`를 파일 분리한다.
   - 이 단계에서는 동작 변경 없이 relocation만 한다.
2. `WorldStateWriteContext`를 top-level type으로 추출한다.
   - `WorldState` explicit mutation port를 먼저 도입한다.
3. 구조 테스트를 새 write-context 구조에 맞게 갱신한다.
4. `TickInputBuffer`를 추가한다.
5. `TickRunner`를 추가한다.
6. `GameplayBootstrapper` / `GameplayCompositionRoot`에 runner 생성 API를 추가한다.
7. runner/buffer unit test를 추가한다.
8. 마지막으로 runtime host 연동이 필요하면 상위 계층에서 thin adapter를 붙인다.

이 순서를 권장하는 이유:

- 먼저 file split만 하면 구조 리스크를 최소화한 채 compile path를 안정화할 수 있다.
- write-context 추출은 구조 테스트 영향이 크므로 runner보다 먼저 고정하는 편이 낫다.
- runner/buffer는 기존 replay/scenario 경로를 깨지 않고 병렬 경로로 추가할 수 있다.

### 15-14. deferred normalization 메모

아래 항목은 target-state에는 있지만 이번 단계에서 바로 만들지 않는다.

- `TerrainData`
  - 현재 movement/attack 판정이 occupancy 중심이라 실질 책임이 없다.
  - terrain rule이 생길 때 `WorldSnapshot` 질의와 함께 도입한다.
- `PlayerLogic`
  - `TickInput` payload 스키마가 아직 최소 상태라 concrete player logic을 확정할 근거가 부족하다.
- `EnemyLogic`
  - 실제 AI 정책이 아직 정의되지 않았으므로 빈 구현 추가는 구조적 가치보다 잡음을 늘릴 가능성이 크다.

이 세 항목은 "결정론 코어가 미완성"을 의미하지 않는다.
현재 남은 것은 우선 runtime composition closure이며, gameplay rule authoring은 다음 슬라이스다.
