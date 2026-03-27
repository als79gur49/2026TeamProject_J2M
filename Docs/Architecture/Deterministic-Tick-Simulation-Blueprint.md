# Deterministic Tick Simulation Blueprint

## 1. 목적

이 문서는 정수 그리드 기반, Fixed Tick 기반, 결정론적 시뮬레이션 구조의 최종 설계 기준을 정의한다.

이 문서는 아래 문서의 상위 구조 원칙을 따른다.

- `Docs/Architecture/Hybrid-Architecture-Rulebook.md`
- `AI_HYBRID_STRUCTURE_RULES.md`

즉, 이 시스템은 범용 인프라가 아니라 게임 규칙이므로 `_Core`가 아니라 `Assets/_Features/Gameplay` 아래에 배치한다.

이 문서는 책임 경계와 안정된 설계 원칙을 정의한다. 현재 파일 배치나 임시 co-location은 `Deterministic-Tick-Simulation-Implementation-Plan.md`의 current-state를 따르며, Unity generated `.csproj`의 explicit compile include 제약 때문에 여러 타입이 같은 `.cs` 파일에 함께 존재할 수 있다.

중요한 판단 기준은 파일 개수보다 `namespace`, `public contract`, `책임 경계`다.

## 2. 핵심 철학

- 엔티티 주도 행동은 `Intent -> ActionGroup 후보 -> Resolver 선택 -> Commit` 흐름을 따른다.
- Phase 간 시스템 유발 결과는 `PhaseTransientBuffer`로 전달된 뒤, 다음 Phase에서 synthetic intent로 정규화되어 같은 경로에 합류한다.
- `ActionGroup`은 원자적이며 `All-or-Nothing`이다.
- 계산 단계는 Snapshot과 phase-local transient data만 읽는다.
- 실제 월드 변경은 Committer만 수행한다.
- Tick은 `Movement -> Attack -> Cleanup` 세 Phase로 고정한다.
- `Movement`는 `S0`, `Attack`은 `S1 + transient buffer` 기준으로 판단한다.
- `Cleanup`에서만 제거와 상태 전이를 확정한다.
- `EntityLogic`은 로직 생산자이며, `WorldState`가 유일한 authoritative state다.
- `View`는 Tick 내부를 모르고 `TickResult`만 소비한다.

## 3. 최종 채택 정책

### 3-1. PhaseTransientBuffer

- `ImpactReservation`은 Entity가 만든 Attack Intent가 아니라 Movement의 물리적 결과다.
- `ImpactReservation`은 `PhaseTransientBuffer`를 통해 Movement에서 Attack으로 전달한다.
- 이 버퍼는 범용 이벤트 버스가 아니라, Tick 내부 phase 간 전달용 임시 저장소로만 사용한다.
- 초기 버전은 `ImpactReservation`만 저장한다.

### 3-2. 1 source -> 1 primary intent per phase

- 한 source가 같은 Phase에서 여러 Intent를 직접 내기 시작하면 Resolver가 조합기 역할까지 떠맡게 된다.
- 같은 Phase에서 한 source는 최대 하나의 entity-generated primary intent만 생성한다.
- 복합 행동이 필요하면 `MoveAndPushIntent`, `MultiAttackIntent`처럼 별도 Intent 타입으로 모델링한다.
- 초기 버전은 복수 intent 지원 대신 복합 intent 타입 확장만 허용한다.

### 3-3. 결정론적 ID 발급

- `intentId`, `groupId`, `spawnId`가 수집 순서에 묶이면 자료구조 순회 순서가 결과를 바꾼다.
- 수집 시점에는 ID 없이 raw data를 모은다.
- 결정론적 정렬 후 중앙 할당기로 ID를 부여한다.
- 카테고리별로 복잡한 분산 ID 시스템을 만들지 않는다.
- Tick 파이프라인 중앙에서만 ID를 할당한다.

### 3-4. 계층형 Occupancy

- 단일 `occupancyByCell`만으로는 Unit과 Projectile의 동시 존재를 자연스럽게 표현하기 어렵다.
- 초기 구현은 `Unit`과 `Projectile` 두 동적 계층만 둔다.
- `Terrain`은 동적 occupancy가 아니라 별도 board or tile data로 분리한다.
- `Effect` 계층은 실제 필요가 생길 때 추가한다.
- 처음부터 범용 `N-layer occupancy graph`를 만들지 않는다.
- 외부에는 계층별 query API만 노출한다.

### 3-5. OccupiesTile / IsTargetable 분리

- 물리 점유와 새 타겟 선택 가능성은 장기적으로 서로 다른 정책이 될 수 있다.
- 저장 필드는 `markedForDeath`만 둔다.
- `BlocksMovement`, `CanBeTargetedForNewSelection`은 중앙 질의 함수로 계산한다.
- 정책이 실제로 갈라질 때까지 bool 필드를 늘리지 않는다.

## 4. 역할 분리

### 4-1. WorldState

실제 시뮬레이션 상태의 유일한 소유자다.

- 엔티티 상태 보관
- 동적 점유 상태 보관
- 제거 예약 상태 보관
- Spawn 결과 반영

초기 권장 내부 구조:

- `entitiesById`
- `unitOccupancy`
- `projectileOccupancy`
- `boardBounds`
- `terrainData`

설명:

- `unitOccupancy`: 플레이어, 적, 소환 유닛
- `projectileOccupancy`: 투사체, 날아가는 오브젝트
- `boardBounds`: 보드 안/밖을 authoritative하게 판정하는 경계 데이터
- `terrainData`: 벽, 바닥, 컨베이어 같은 정적 혹은 준정적 보드 데이터

현재 구현 메모:

- 샘플 씬의 외벽처럼 보이는 일부 blocker는 아직 terrain이 아니라 `EntityType.None` entity wall이다.
- 따라서 중앙 이동 질의는 `board bounds + terrain + blocking entity`를 모두 보고, entity wall도 valid stopper로 유지한다.

`effectOccupancy`는 실제 필요가 생길 때 추가한다.

### 4-2. EntityLogic

Snapshot을 읽고 Intent를 생산하는 계층이다.

- 플레이어 입력 해석
- 적 AI
- 터렛 자동 공격
- 투사체 이동 의도 생성

중요 규칙:

- `EntityLogic`은 live world를 읽지 않는다.
- `EntityLogic`은 월드를 직접 쓰지 않는다.
- `EntityLogic`은 Phase별 Intent만 생산한다.
- 같은 Phase에서 한 source는 최대 하나의 entity-generated primary intent만 생성한다.

dynamic entity 복구/조립 규칙:

- `IEntityLogicSourceBinding`은 어떤 entity/phase를 이미 제어하는지 선언한다.
- `IEntityLogicFactory`는 `EntityState`를 concrete `IEntityLogic`로 materialize한다.
- `ISnapshotEntityLogicProvider`는 snapshot을 읽고 이번 tick의 dynamic `IEntityLogic` 집합을 구성한다.
- static `IEntityLogic`와 dynamic `IEntityLogic`의 phase ownership 충돌 판단은 provider가 맡는다.
- `GameplayCompositionRoot`와 `GameplayBootstrapper`는 provider와 pipeline 조립 책임을 가진다.
- `TickPipeline`은 provider를 사용만 하고, concrete factory를 직접 조립하지 않는다.

### 4-3. Committer

유일한 쓰기 계층이다.

- `MovementCommitter`
- `AttackCommitter`
- `CleanupProcessor`

이 셋만 `WorldState`를 변경할 수 있다.

### 4-4. View

Unity `GameObject`, `Transform`, `Animator`, `VFX`, `SFX`는 전부 View 계층이다.

- Tick 중간 개입 금지
- `TickResult` 기반 시각화
- 보간과 애니메이션은 View 책임

## 5. 데이터 모델

### 5-1. EntityState

Snapshot과 WorldState에 공통으로 쓰는 순수 상태 값이다.

```csharp
public struct EntityState
{
    public int entityId;
    public Vector2Int position;
    public int hp;
    public int maxHp;
    public int teamId;
    public EntityType type;
    public EntityPhaseState state;
    public int stateTimer;
    public Direction facing;
    public bool markedForDeath;
    public int spawnTick;
}
```

중요 규칙:

- `markedForDeath`는 Cleanup 예약 상태다.
- 실제 제거 전까지는 WorldState에 남아 있을 수 있다.
- `OccupiesTile`, `IsTargetable`은 저장 필드가 아니라 중앙 질의 함수로 계산한다.

초기 중앙 질의 규칙:

- `BlocksMovement(entity)`: Cleanup 전까지 true
- `CanBeTargetedForNewSelection(entity)`: 초기 버전에서는 `!markedForDeath`

### 5-2. Intent

Intent는 의도이며 결과가 아니다.

```csharp
public abstract class Intent
{
    public int intentId;
    public int sourceId;
    public int priority;
}
```

중요 규칙:

- 하나의 `intentId`는 Tick 내에서 유일해야 한다.
- `EntityLogic`은 ID를 직접 발급하지 않는다.
- Intent는 먼저 ID 없이 수집한 뒤 정렬 후 중앙에서 ID를 할당한다.

초기 규칙:

- 같은 source는 같은 Phase에 최대 하나의 primary intent만 생성한다.
- 복합 행동은 별도 복합 Intent 타입으로 표현한다.

Unity 구현 배치 기준:

- `Intent` base type은 특정 feature가 아니라 공용 실행 모델 레이어에 둔다.
- `MoveIntent`, `AttackIntent` 같은 concrete intent만 각 feature 레이어에 둔다.
- 공용 intent 정렬기는 공용 모델 레이어에서 관리한다.

### 5-3. ActionGroup

Commit 가능한 원자적 결과 묶음이다.

```csharp
public class ActionGroup
{
    public int groupId;
    public int intentId;
    public int sourceId;
    public int priority;

    public List<MoveAction> moves = new();
    public List<DamageAction> damages = new();
    public List<SpawnAction> spawns = new();
    public List<DestroyAction> destroys = new();
    public List<StateChangeAction> stateChanges = new();
}
```

중요 규칙:

- 부분 성공 금지
- Resolver는 선택만 하고 수정하지 않는다
- Committer는 선택된 그룹만 적용한다
- `groupId`도 정렬 후 중앙에서 부여한다

Unity 구현 배치 기준:

- `ActionGroup`과 `ActionGroupKind`는 특정 feature 소유가 아니라 공용 실행 모델 레이어에 둔다.
- `MoveAction`, `DamageAction`, `DestroyAction`, `SpawnAction`, `StateChangeAction`도 `ActionGroup`과 함께 공용 모델로 관리한다.
- `ActionGroupComparer`는 Movement/Attack 양쪽 phase가 공유하는 총정렬 계약으로 취급한다.

### 5-4. WorldSnapshot

읽기 전용 기준 상태다.

```csharp
public class WorldSnapshot
{
    public BoardBounds BoardBounds { get; }
    public IReadOnlyDictionary<int, EntityState> entitiesById;
    public IReadOnlyDictionary<Vector2Int, int> unitOccupancy;
    public IReadOnlyDictionary<Vector2Int, int> projectileOccupancy;

    public bool TryGetUnitAt(Vector2Int cell, out EntityState entity);
    public bool TryGetProjectileAt(Vector2Int cell, out EntityState entity);
    public bool IsInsideBoard(Vector2Int cell);
    public bool IsBlockedForUnit(Vector2Int cell);
    public bool TryGetBoxSlideDestination(
        Vector2Int origin,
        Vector2Int delta,
        out Vector2Int destination,
        out SlideStopper stopper);
}
```

중요 규칙:

- Snapshot은 생성 후 불변이다.
- Snapshot은 `EntityLogic`이나 `MonoBehaviour`를 참조하지 않는다.
- Snapshot 순회는 결정론적 정렬 버퍼를 통해 수행한다.
- 외부 호출부는 내부 저장 구조를 직접 알지 않는다.
- `IsBlockedForUnit`은 `board bounds + terrain blocker + blocking entity`를 함께 본다.
- projectile layer는 `IsBlockedForUnit`과 box slide stopper에서 제외한다.
- `MovementExpander`는 직접 entity ray scan을 하지 않고, `WorldSnapshot` / `WorldQueryService`의 중앙 질의만 사용한다.

### 5-5. ImpactReservation

Movement의 물리적 결과를 Attack으로 넘기는 system-generated input이다.

```csharp
public struct ImpactReservation
{
    public int sourceId;
    public int targetId;
    public Vector2Int position;
    public int damage;
    public int tickGenerated;
    public int sourceActionGroupId;
    public int reservationSequence;
}
```

중요 규칙:

- Entity가 생성한 attack intent가 아니다.
- Movement Commit의 적용 순서에서만 생성된다.
- `reservationSequence`는 동일 Tick 안에서 결정론적 순서를 위한 값이다.
- Attack Phase 시작 시 attack input 정규화 과정에서 synthetic `intentId`를 부여한다.

### 5-6. PhaseTransientBuffer

Tick 내부 phase 간 전달용 임시 저장소다.

```csharp
public sealed class PhaseTransientBuffer
{
    private readonly List<ImpactReservation> impactReservations = new();

    public void AddImpact(ImpactReservation reservation) { ... }
    public List<ImpactReservation> DrainImpacts() { ... }
}
```

중요 규칙:

- `WorldState`와 분리한다.
- Snapshot에는 포함하지 않는다.
- 다음 Tick으로 자동 이월하지 않는다.
- Drain 순서는 결정론적이어야 한다.
- 초기 버전은 `ImpactReservation` 전달에만 사용한다.

## 6. 결정론 규칙과 ID 발급

모든 Resolver와 Committer는 아래 총정렬 키를 사용한다.

```text
priority desc
sourceId asc
intentId asc
groupId asc
```

중요 구현 규칙:

- `Dictionary`, `HashSet` 순회 순서에 의존하지 않는다.
- `List.Sort`의 안정성에 의존하지 않는다.
- 비교 키는 항상 총정렬이 되도록 끝까지 채운다.

### 6-1. Intent ID 발급 절차

1. ID 없는 raw intent 수집
2. `sourceId` 기준 결정론적 정렬
3. 중앙 `IdAllocator`로 `intentId` 부여

### 6-2. Attack input 정규화와 intent ID 발급 절차

1. raw attack intent 수집
2. `PhaseTransientBuffer`에서 reservation 추출
3. 두 입력을 하나의 attack input 목록으로 정규화
4. `sourceId asc -> inputKind asc -> localSequence asc`로 정렬
5. 중앙 `IdAllocator`로 `intentId` 부여
6. reservation은 이 단계에서 synthetic `intentId`를 얻고, 이후 entity intent와 같은 식별 규칙으로 취급

정규화 키 정의:

- `inputKind`: `EntityIntent = 0`, `ImpactReservation = 1`
- `localSequence`: entity intent는 0, reservation은 `reservationSequence`

### 6-3. ActionGroup ID 발급 절차

1. Expander가 raw candidate 생성
2. 후보를 의미론적 우선순위와 정렬 키로 재정렬
3. 중앙 `IdAllocator`로 `groupId` 부여

### 6-4. Spawn ID 발급 절차

1. 선택된 `SpawnAction`을 Commit 순서로 정렬
2. 중앙 `IdAllocator`로 `spawnId` 부여
3. WorldState에 Spawn 반영

## 7. Phase별 인터페이스

기존의 `GenerateIntent(snapshot)` 단일 메서드는 사용하지 않는다.

```csharp
public interface IEntityLogic
{
    void CollectMovementIntents(
        in WorldSnapshot snapshot,
        in TickInput input,
        List<Intent> buffer);

    void CollectAttackIntents(
        in WorldSnapshot snapshot,
        List<Intent> buffer);
}
```

이 구조가 강제하는 것:

- Movement Intent는 `S0`만 읽는다.
- Attack Intent는 `S1`만 읽는다.
- 같은 엔티티가 한 Tick에 이동과 공격을 모두 할 수 있다.
- 실제 가능 여부는 `state`, `cooldown`, `windup`, `recovery` 규칙이 결정한다.
- 같은 Phase에서 한 source는 최대 하나의 primary intent만 생성한다.

## 8. Tick 흐름

```text
TickStart
  -> Snapshot S0 생성

  [Movement Phase]
  -> raw movement intent 수집
  -> intent 정렬 및 ID 부여
  -> movement 후보 생성
  -> candidate 정렬 및 groupId 부여
  -> MovementResolver 선택
  -> MovementCommitter 적용
  -> ImpactReservation 기록

  -> Snapshot S1 생성

  [Attack Phase]
  -> raw attack intent 수집
  -> transientBuffer.DrainImpacts()
  -> attack input 정규화 및 intentId 부여
  -> attack 후보 생성
  -> candidate 정렬 및 groupId 부여
  -> AttackResolver 선택
  -> AttackCommitter 적용

  [Cleanup Phase]
  -> 제거 확정
  -> stateTimer 감소
  -> 상태 전이
  -> TickResult 생성
TickEnd
```

## 9. Movement Phase 정책

### 9-1. 역할

- `Move`
- `Push`
- `Throw`
- `ProjectileMove`

### 9-2. 입력 기준

- `TickStartSnapshot(S0)`

### 9-3. Expander

Expander는 하나의 Intent를 여러 `ActionGroup` 후보로 확장한다.

예:

- `MoveIntent(Move) -> Move / Stop`
- `InteractIntent -> BoxSlide / Fail`
- `ThrowIntent -> Throw / Fail`
- `ProjectileMoveIntent -> ProjectileMove / ImpactReservation`

후보 생성 규칙:

- 하나의 Intent에서 파생되는 후보 분기는 의미론적 우선순위로 고정한다.
- Intent 타입별 분기 순서는 문서와 테스트로 고정한다.
- 후보 생성 후 정렬하고 `groupId`를 부여한다.

### 9-4. Direct Move Blocking 정책

- 일반 `Move`는 점유된 `Unit`이나 `Box`를 밀지 않는다.
- 플레이어가 생성한 `Move`는 box slide로도 자동 승격되지 않는다.
- box 위치 변경은 `Interact(BoxSlide)`와 `Throw`로만 처리한다.
- blocked destination이면 전체 실패다.
- 같은 Intent에서는 최대 하나의 `ActionGroup`만 선택된다.

### 9-5. Box Interaction 정책

- `Box` 능력은 분산 bool이 아니라 `BoxCapabilities` flag로 표현한다.
- 플레이어가 밀 수 있는 것은 오직 `Box`다.
- 플레이어의 `Move`는 `Unit`도 `Pushable` 박스도 밀지 않는다.
- `Move`는 `Pushable` 박스를 자동으로 밀지 않는다.
- `Movement` phase는 `Interact`에 의한 single-target `BoxSlide`와 `Throwable` 박스에 대한 `Throw`만 처리한다.
- `Interact`는 인접 `Pushable` 박스 1개만 대상으로 삼는다.
- `BoxSlide` 목적지 계산은 `WorldSnapshot` / `WorldQueryService`의 중앙 질의가 담당한다.
- `BoxSlide` stopper는 `BoardEdge -> Terrain -> Entity` 순서로 판정한다.
- projectile은 `BoxSlide` stopper가 아니다.
- `BoxSlide`는 interaction 방향 ray 위의 첫 stopper 직전까지 박스를 이동시킨다.
- bounded board에서는 stopper 없음 상태가 원칙적으로 발생하지 않는다.
- stopper가 대상 박스에 인접해 있으면 slide는 실패한다.
- `BoxSlide` 동안 player source는 anchor cell에 남는다.
- `Throw`는 source entity를 고정한 채 인접 박스를 source 반대편 인접 cell로 이동시키는 movement 확장이다.
- `Throw` 성공/실패는 `S0` 기준으로만 판정한다.
- `Interact`는 `Movement`와 `Attack`에 모두 걸치지만, box slide는 `Movement`, loot-destroy는 `Attack`이 처리한다.
- `LootOnInteractDestroy`가 있는 박스에 대한 `Interact` 성공 시 loot 이벤트와 `MarkDestroy`만 기록한다.
- 실제 제거와 occupancy 정리는 반드시 `Cleanup`에서만 수행한다.
- 현재 sample scene의 `EntityType.None` blocker wall은 terrain wall이 아니라 entity stopper로 취급한다.

### 9-6. Projectile 정책

투사체의 이동 자체는 Movement에서 처리한다.

권장 정책:

1. `ProjectileMoveIntent` 생성
2. Movement Expander가 경로 충돌을 검사
3. 충돌이 발생하면 `ImpactReservation`을 남긴다
4. 실제 피해와 파괴는 Attack Phase에서 처리한다

즉, Movement는 `위치/경로/점유`, Attack은 `피해/파괴/상태이상`에 집중한다.

초기 제약:

- 하나의 투사체는 한 Tick Movement에서 최대 하나의 `ImpactReservation`만 남긴다.
- 다중 관통은 추후 별도 정책으로 연다.

### 9-7. Movement Commit

Movement Commit은 선택된 이동 그룹만 적용한다.

- 위치 변경
- facing 변경
- 이동 관련 이벤트 기록
- `ImpactReservation` 기록

이 단계에서 직접 damage를 적용하지 않는다.

## 10. Attack Phase 정책

### 10-1. 역할

- `Interact`
- `Attack`
- `Laser`
- `FireProjectile`
- `CastStart`
- `CastRelease`
- `ImpactReservation` 소비

### 10-2. 입력 기준

- `PostMoveSnapshot(S1)`
- `PhaseTransientBuffer.DrainImpacts()`

### 10-3. 수집 규칙

- Attack Phase 시작 시 살아 있는 엔티티만 Attack Intent를 생성할 수 있다.
- Attack Commit 중간에 죽더라도 이미 생성된 후보는 유지한다.
- 같은 엔티티가 이동 후 공격하는 것은 허용한다.
- `InteractIntent`는 플레이어 입력이 직접 생성한 raw intent만 사용한다.
- `ImpactReservation`은 EntityLogic이 생성하는 Attack Intent가 아니라 Movement 결과로 넘어오는 system-generated input이다.
- Attack Phase에서 Spawn된 엔티티는 같은 Tick에 새 Intent를 생성하지 않는다.

Attack Expander 입력:

- entity-generated attack intents
- system-generated `ImpactReservation`

둘은 출처가 다르지만, Attack input 정규화 단계에서 동일한 `intentId` 규칙으로 묶인 뒤 Expander에 들어간다.
즉, Expander 이후에는 모두 동일한 식별 규칙을 가진 `ActionGroup` 후보로 통합된다.

### 10-4. Resolver

AttackResolver는 MovementResolver와 다르다.

- 동일 target에 대한 damage는 허용한다.
- damage 누적은 허용한다.
- 같은 `intentId`에서는 최대 하나의 후보만 선택한다.
- destroy 중복은 제거한다.

### 10-5. Commit 순서

Attack Commit의 고정 순서는 아래로 정의한다.

1. `StateChange`
2. `Damage`
3. `Spawn`
4. `DestroyMark`

설명:

- `StateChange`: 공격자/피격자의 상태 반영
- `Damage`: HP 감소와 누적 처리
- `Spawn`: 투사체, 장판, 소환물 생성
- `DestroyMark`: 실제 제거는 하지 않고 Cleanup 예약만 남김

이 단계에서는 월드에서 제거하지 않는다.

### 10-6. HP 0 점유 정책

- `hp <= 0` 또는 `DestroyMark` 상태여도 Cleanup 전까지는 타일을 점유한다.
- 같은 Attack Phase 중간에 빈칸으로 취급하지 않는다.
- 실제 제거는 Cleanup에서만 발생한다.

초기 중앙 질의 규칙:

- `BlocksMovement(entity)`: Cleanup 전까지 true
- `CanBeTargetedForNewSelection(entity)`: 초기 버전에서는 `!markedForDeath`

즉, 물리 점유와 새 타겟 선택 가능 여부는 개념적으로 분리한다.

## 11. Cleanup Phase 정책

Cleanup은 Tick의 마감 단계다.

실행 순서:

1. `hp <= 0` 또는 `DestroyMark` 대상 제거
2. 점유 테이블 갱신
3. `stateTimer` 감소
4. 상태 전이 수행
5. Cleanup 이벤트 기록
6. `TickResult` 생성

핵심 정책:

- 제거는 Cleanup에서만 일어난다.
- `stateTimer == 0`의 효력은 다음 Tick부터 발생한다.
- Spawn된 엔티티는 생성 Tick의 Cleanup에서 타이머를 감소시키지 않는다.
- Cleanup에서 바뀐 상태는 같은 Tick 안에서 다시 사용되지 않는다.

## 12. TickResult

`TickResult`는 최종 월드 상태와 이벤트 로그를 분리해 전달한다.

최소 포함 항목:

- `move`
- `damage`
- `spawn`
- `destroy`
- `state change`

권장 추가 항목:

- `blocked`
- `rejected`
- `projectile impact`
- `cleanup removed`
- `cast start`
- `cast release`
- `death marked`

View는 `WorldState` 직접 참조가 아니라 `TickResult`를 기반으로 연출을 재생한다.

## 13. Unity 구현 배치

권장 폴더 구조:

```text
Assets/_Features/Gameplay/
  Gameplay_Model/
  Gameplay_Loop/
  Gameplay_BoardState/
  Gameplay_Movement/
  Gameplay_Attack/
  Gameplay_Cleanup/
  Gameplay_Entities/
```

권장 책임:

- `Gameplay_Model`: TickPhase, Intent, ActionGroup, ActionGroupKind, 공용 Actions, IntentComparer, ActionGroupComparer
- `Gameplay_Loop`: TickRunner, InputBuffer, IdAllocator, TickPipeline, TickResultBuilder, PhaseTransientBuffer, DelayedAttackEffectQueue, GameplayCompositionRoot, GameplayBootstrapper
- `Gameplay_BoardState`: WorldState, WorldSnapshot, SnapshotBuilder, BoardBounds, TerrainData, SlideStopper, WorldQueryService
- `Gameplay_Movement`: MoveIntent, raw movement collection, Expanders, Resolver, Committer
- `Gameplay_Attack`: AttackIntent, raw attack collection, Expanders, Resolver, Committer, ImpactReservationExpander
- `Gameplay_Cleanup`: CleanupProcessor, StateTransitionProcessor
- `Gameplay_Entities`: IEntityLogic, IEntityLogicSourceBinding, IEntityLogicFactory, ISnapshotEntityLogicProvider, SnapshotEntityLogicProvider, GameplayEntityLogicProviderFactory, PlayerLogic, EnemyLogic, TurretLogic, ProjectileLogic, ProjectileEntityLogicFactory

현재 구현 메모:

- 책임상 위와 같이 해석하지만, generated `.csproj` 제약 때문에 일부 type은 기존 파일에 co-locate될 수 있다.
- 예를 들어 `SnapshotEntityLogicProvider`, `GameplayEntityLogicProviderFactory`, `GameplayCompositionRoot`, `GameplayBootstrapper`는 현재 `TickPipeline.cs` 안에 존재할 수 있다.
- 이 경우에도 `TickPipeline` 클래스 자체가 orchestration-only 책임을 유지하는지가 판단 기준이다.

## 14. 잔여 리스크와 보완 필요

아래 항목은 현재 설계를 채택한 뒤에도 장기적으로 주의해야 할 지점이다.

### 14-1. 후보 분기 순서의 명시성

`1 source -> 1 primary intent`를 채택해도, Expander 내부 분기 순서가 흔들리면 여전히 결과가 달라질 수 있다.

예:

- `Move` 후보를 먼저 내는가
- `BoxSlide` 후보를 먼저 내는가
- `Stop` 후보를 언제 추가하는가

권장:

- Expander별 분기 우선순위를 문서와 테스트로 고정한다.

### 14-2. Movement 경로 충돌의 의미론

같은 타일 점유 충돌만으로는 부족할 수 있다.

예:

- swap 허용 여부
- head-on crossing 허용 여부
- 속도 2 이상 경로 교차

권장:

- `destination reservation` 외에 `edge reservation` 개념을 넣을 준비를 한다.

### 14-3. Attack StateChange의 세분화

현재는 `StateChange -> Damage -> Spawn -> DestroyMark`로 고정했지만,
향후 상태 변화가 복잡해지면 `StateChange`를 둘로 나눌 필요가 있다.

예:

- 공격자 Recovery 진입
- 피격자 HitStun 진입
- 생존 시에만 발동하는 상태 변화

권장:

- 장기적으로는 `PreDamageStateChange`와 `PostDamageStateChange` 분리를 고려한다.

### 14-4. 사망 직후 대상성의 장기 변화

초기 버전은 `BlocksMovement`와 `CanBeTargetedForNewSelection`로 충분하다.
하지만 시체 장애물, 부활 대기 상태 같은 규칙이 생기면 추가 세분화가 필요하다.

예:

- 시체는 길을 막지만 공격 대상은 아닌가
- 다운 상태는 공격 대상이지만 밀릴 수는 없는가

권장:

- 상태 플래그를 늘리기 전에 중앙 질의 함수부터 확장한다.

### 14-5. Reaction과 On-Hit 파생 효과

현재 설계는 Commit 중간 새 Intent 생성을 금지한다.
따라서 아래 기능은 현재 모델 밖에 있다.

- 피격 즉시 반격
- 처치 즉시 추가 공격
- on-hit 즉시 teleport

권장:

- 초기 버전에서는 금지
- 필요하면 다음 Tick 지연형 이벤트로만 모델링

### 14-6. 입력 샘플링 경계

플레이어 입력이 렌더 프레임 기준으로 들어오면 Tick 경계에서 흔들릴 수 있다.

권장:

- 입력은 Tick 인덱스에 귀속된 `TickInputBuffer`에 먼저 기록
- 시뮬레이션은 버퍼된 입력만 소비

## 15. 구현 우선순위

1. `WorldState`, `WorldSnapshot`, `TickPipeline`, `TickResult` 뼈대 작성
2. `PhaseTransientBuffer`, raw intent 수집, post-sort ID 발급 구현
3. `Unit` / `Projectile` 2계층 occupancy와 movement blocking 규칙 구현
4. Attack Phase와 `ImpactReservation` 소비 구현
5. Cleanup과 상태 전이 구현
6. ViewBridge와 리플레이 테스트 구축
7. Projectile 고속 경로 정책 확장

## 16. 최종 요약

이 설계의 핵심은 아래 다섯 줄이다.

- 읽기는 Snapshot, 쓰기는 Committer
- Tick은 `Movement -> Attack -> Cleanup`
- `EntityLogic`은 Intent 생산자, `WorldState`는 상태 소유자
- `ImpactReservation`은 `PhaseTransientBuffer`를 통해 Movement에서 Attack으로 전달된다
- 결정론은 자료구조가 아니라 명시적 정렬 키, post-sort ID 발급, 고정된 Phase 정책으로 보장한다
