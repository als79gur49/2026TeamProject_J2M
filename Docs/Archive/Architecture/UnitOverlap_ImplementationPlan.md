> Archived historical document.
> This file is not part of the active truth-source chain. Start with [Docs/Architecture/README.md](../../Architecture/README.md).
> Archive index: [Docs/Archive/README.md](../README.md).

> Non-canonical implementation-history document.
> Layered occupancy and query vocabulary now follow [Tick-Simulation-Canonical-Spec.md](../../Architecture/Tick-Simulation-Canonical-Spec.md).

# Unit Overlap Implementation Plan

## 목적

이 문서는 Gameplay 시스템에 `Unit` 간 동일 타일 중첩을 도입하기 위한 구현 계획을 정의한다.

이번 변경의 핵심 요구사항은 다음과 같다.

- `플레이어와 적은 반드시 같은 타일에 동시에 존재할 수 있어야 한다.`
- `적과 적도 같은 타일에 동시에 존재할 수 있어야 한다.`
- 이는 일부 적이 공격 액션 없이 `충돌 자체`로 피해를 주는 게임 규칙을 성립시키기 위해 필수다.
- 반면 `Box`, `Wall(EntityType.None)`, `RemovedEntity`의 기존 퍼즐/차단 규칙은 최대한 유지한다.

즉, 이번 변경은 `적 길막 해소` 수준이 아니라 `Unit끼리의 같은 타일 공유를 공식 규칙으로 승격`하는 작업이다.

## 최종 규칙

- `Unit + Unit`: 허용
- `Player(Unit) + Enemy(Unit)`: 허용
- `Enemy(Unit) + Enemy(Unit)`: 허용
- `Unit + Box`: 금지
- `Unit + Wall(EntityType.None)`: 금지
- `Box + Box`: 금지
- `RemovedEntity + RemovedEntity`: 기존 규칙 유지
- `RemovedEntity + Unit`: 기존 로직 유지, 단 stacked unit 중 누구를 맞추는지 명시적으로 결정

## 설계 원칙

- 월드 상태는 `중첩 가능한 점유`와 `중첩 불가능한 점유`를 분리한다.
- `TryGetUnitAt` 같은 단일 반환 API에 의미를 과도하게 실지 않는다.
- 같은 타일에 여러 Unit이 존재할 때에도 모든 선택 결과는 결정론적이어야 한다.
- 이동 로직과 전투 로직의 규칙 차이를 명시적으로 분리한다.
- 프레젠테이션은 로직 변경의 부산물이 아니라 별도 완료 항목으로 다룬다.

## 비목표

- 이번 계획의 1차 목표는 `Unit 겹침 허용`이다.
- `Box` 다중 점유 허용은 하지 않는다.
- 큐브 회전 규칙의 동시성 모델은 바꾸지 않는다.
- 광역 공격, 스플래시, 관통 규칙 추가는 범위 밖이다.

## 핵심 문제 재정의

현재 시스템은 사실상 `한 타일 = 비투사체 1개`를 전제로 설계되어 있다.

영향 영역은 다음과 같다.

- 월드 저장 구조
- 배치 합법성 검사
- 이동 확장
- 이동 충돌 해결기
- 투사체 충돌 타깃 결정
- 근접 공격 거리 판정
- 디버그 덤프 및 determinism hash
- 동일 타일 시각화

따라서 단일 함수 수정으로 해결할 수 없다. 단계별로 불변식을 옮겨야 한다.

## 구현 단계

### 0단계. 요구사항 고정

목표

- 설계 기준을 문서로 고정한다.
- 논쟁의 여지를 줄이기 위해 `모든 Unit-Unit 중첩 허용`을 1차 요구사항으로 확정한다.
- 특히 `플레이어-적 same-cell`은 절대 후퇴 불가한 대표 필수 시나리오로 고정한다.

작업

- 본 문서를 기준 문서로 사용한다.
- 코드 구현 전 다음 문장을 팀 공용 규칙으로 확정한다.
- `Unit은 다른 Unit과 같은 타일에 존재할 수 있다.`
- `이는 Player-Enemy, Enemy-Enemy를 모두 포함한다.`
- `동일 타일 충돌은 공격 판정과 접촉 판정의 유효 상태다.`
- `Player-Enemy same-cell은 대표 필수 검증 시나리오이며, Enemy-Enemy same-cell도 동일한 지원 범위에 포함된다.`

완료 기준

- 구현자가 더 이상 `같은 팀만 허용` 또는 `플레이어-적만 우선 지원` 같은 축소 해석을 하지 않는다.

### 1단계. 월드 점유 모델 분리 [완료]

목표

- `Unit`을 다중 점유 가능한 레이어로 옮긴다.
- `Box`와 `Wall`은 기존처럼 단일 차단 레이어로 유지한다.

대상 파일

- [WorldState.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs)
- [WorldSnapshot.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldSnapshot.cs)
- [SnapshotReadQueries.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/Queries/SnapshotReadQueries.cs)
- [WorldPlacementPolicy.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/Queries/WorldPlacementPolicy.cs)

작업

- `_unitOccupancy : Dictionary<SurfaceCell, int>`를 폐기한다.
- 아래 구조로 분리한다.
- `stackedUnitsByCell : Dictionary<SurfaceCell, SortedSet<int>>`
- `solidOccupancyByCell : Dictionary<SurfaceCell, int>` for `Box`, `EntityType.None`
- `removed entityOccupancyByCell : Dictionary<SurfaceCell, int>`
- `Unit` 추가/이동/제거 시 set 기반으로 갱신한다.
- `Box`와 `Wall`은 기존처럼 단일 occupant로 유지한다.
- `SortedSet<int>` 또는 entityId 정렬된 리스트를 사용해 순서를 결정론적으로 유지한다.

주의점

- `EntityType.Unit`과 `EntityType.Box`가 모두 예전 `_unitOccupancy`를 쓰고 있으므로, 이번 단계에서 반드시 의미를 분리해야 한다.
- 그래야 이후 단계에서 `box 질의`와 `unit 질의`를 따로 만들 수 있다.

완료 기준

- 동일 cell에 `Unit` 두 개를 spawn/move해도 world state invariant 예외가 발생하지 않는다.
- 동일 cell에 `Box` 두 개는 여전히 예외가 발생한다.

주요 구현 내용

- `WorldState`의 기존 `_unitOccupancy`를 제거하고 `stackedUnitsByCell`, `solidOccupancyByCell`, `removed entityOccupancyByCell`로 분리했다.
- `Unit`은 `SortedSet<int>` 기반 stack 레이어로 관리되며, authoritative placement에서는 `Unit + Unit` same-cell을 허용한다.
- `Box`, `Wall(EntityType.None)`은 `solidOccupancyByCell`에 남겨 단일 occupant 규칙을 유지했고, `Box + Box`, `Box + Unit`은 여전히 invariant 예외가 발생한다.
- `WorldSnapshot`/`SnapshotReadQueries`는 새 점유 모델을 읽도록 변경했고, 기존 callsite 호환을 위해 `TryGetUnitAt`는 당분간 `solid 우선 + stacked unit 대표값` 조회로 유지했다.
- `WorldStatePlacementInvariantTests`를 갱신해 unit stacking 허용, inactive face authoritative stacking 허용, reoccupy 허용, solid stacking 금지를 검증했다.

### 2단계. Snapshot 질의 API 재설계 [완료]

목표

- 단일 점유자 반환 API 의존을 제거한다.
- 각 사용처가 실제 의도에 맞는 질의를 쓰게 한다.

대상 파일

- [WorldSnapshot.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldSnapshot.cs)
- [WorldQueryService.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldQueryService.cs)
- [SnapshotReadQueries.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/Queries/SnapshotReadQueries.cs)

추가/변경 API 제안

- `HasAnyUnitAt(SurfaceCell cell)`
- `EnumerateUnitsAt(SurfaceCell cell, List<EntityState> buffer)`
- `TryGetPrimaryUnitAt(SurfaceCell cell, out EntityState entity)`
- `TryGetBoxAt(SurfaceCell cell, out EntityState entity)`
- `TryGetSolidOccupantAt(SurfaceCell cell, out EntityState entity)`
- `TryPickImpactTargetAt(SurfaceCell cell, int sourceTeamId, out EntityState entity)`
- `TryPickHostileUnitImpactTargetAt(SurfaceCell cell, int sourceTeamId, out EntityState entity)`

작업

- `TryGetUnitAt`는 하위 호환이 필요하면 남기되, 새 코드에서는 사용 금지 대상으로 본다.
- `TryGetUnitAt`가 꼭 필요하면 `대표 unit 반환`으로 한정하고 주석으로 의미를 명시한다.
- `EnumerateUnitOccupancyOrdered`는 같은 cell에 여러 entry를 출력하도록 바꾼다.

완료 기준

- `Box 찾기`, `Unit 찾기`, `충돌 타깃 선택`이 서로 다른 함수로 나뉜다.

주요 구현 내용

- `WorldSnapshot`/`SnapshotReadQueries`/`WorldQueryService`에 `HasAnyUnitAt`, `EnumerateUnitsAt`, `TryGetPrimaryUnitAt`, `TryGetBoxAt`, `TryGetSolidOccupantAt`, `TryPickImpactTargetAt`를 추가했다.
- box impact 전용으로 `TryPickHostileUnitImpactTargetAt`를 추가해 `friendly fallback 없음 + hostile unit only + entityId 오름차순` 선택을 고정했다.
- `TryGetUnitAt`는 삭제하지 않고 `primary board occupant` 의미로 유지했으며, 새 코드에서는 explicit query API를 사용하도록 주석과 callsite를 정리했다.
- stacked unit query의 공통 표현을 `IReadOnlyCollection<int>` 기준으로 맞춰 authoritative `SortedSet<int>`와 snapshot `ReadOnlyCollection<int>`를 같은 query 계층에서 읽을 수 있게 정리했다.
- `MovementExpander`, `PlayerControlState`, `EnemyMovementPolicy`, `MovementCommitter`는 각각 `box 조회`, `solid 조회`, `impact target 선택`에 맞는 전용 snapshot query를 사용하도록 변경했다.
- `TryPickImpactTargetAt`는 `solid occupant 우선`, stacked unit만 있는 경우 `hostile unit 우선 + entityId 오름차순 fallback`으로 결정되게 구현했고, removed entity impact 예약 생성도 같은 API를 타도록 맞췄다.
- `WorldSurfaceQueryTests`, `MovementPhaseScenarioTests`에 explicit query API와 stacked cell removed entity target selection 경로를 고정하는 테스트를 추가했다.

### 3단계. 배치 차단 규칙 재정의 [완료]

목표

- `Unit`의 이동 가능성과 `Box/RemovedEntity`의 배치 가능성을 분리한다.

대상 파일

- [WorldPlacementPolicy.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/Queries/WorldPlacementPolicy.cs)

새 규칙

- `Unit` 배치 차단
- 보드 밖
- terrain
- solid occupant
- `RemovedEntity` 배치 차단
- 보드 밖
- terrain
- solid occupant
- removed entity occupant
- impact targetable unit 존재 여부는 이동 단계에서 처리
- `Box / Wall(None)` 배치 차단
- 보드 밖
- terrain
- solid occupant
- any unit 존재

설명

- `Unit`이 다른 `Unit`에 의해 막히면 이번 변경의 의미가 없어진다.
- 반대로 `Box`가 stacked unit 위로 올라갈 수 있게 만들면 퍼즐 규칙이 무너진다.

완료 기준

- `WorldSnapshot.IsBlockedForUnit()`가 `stacked unit만 있는 칸`에 대해 `false`를 반환한다.
- `Box` landing/slide는 stacked unit이 있으면 여전히 차단된다.

주요 구현 내용

- `WorldPlacementPolicy.TryGetBlockingPlacementEntity`를 entity type별 switch로 재정의해 `Unit`, `RemovedEntity`, `Box/Wall(None)`의 차단 대상을 명시적으로 분리했다.
- `Unit` placement/gameplay blocker는 이제 `solid occupant`만 차단하며, stacked unit이나 marked-for-death unit만 있는 칸에 대해서는 `IsBlockedForUnit()`/`TryGetPlacementBlocker(EntityType.Unit, ...)`가 `false`를 반환한다.
- `RemovedEntity` placement blocker는 `removed entity occupant`와 `solid occupant`만 차단하도록 바꿔 unit이 서 있는 칸에도 spawn/occupy가 가능하게 했고, 실제 target 선택은 기존 `TryPickImpactTargetAt` + movement impact reservation 경로로 넘겼다.
- `Box / Wall(None)` placement blocker는 stacked unit을 계속 차단하도록 유지했고, `WorldSnapshot.TryResolveNextSurfaceBoxSlideStep()` 테스트로 stacked unit 위 slide stop을 고정했다.
- `WorldStatePlacementInvariantTests`, `TickPipelineStageOneTests`, `WorldSurfaceQueryTests`, `AttackPhaseScenarioTests`를 갱신해 removed entity-on-unit spawn 허용, removed entity-on-solid 금지, unit placement unblock, box slide-on-stacked-unit block을 검증했다.

### 4단계. 이동 확장 로직 수정 [완료]

목표

- 일반 이동은 다른 Unit이 있어도 성공하게 만든다.
- Box 상호작용은 기존처럼 유지한다.

대상 파일

- [MovementExpander.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs)
- [PlayerControlState.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlState.cs)

작업

- `ExpandMoveLike`에서 목적지 검사 순서를 바꾼다.
- 먼저 `TryGetBoxAt(destinationCell)` 또는 `TryGetSolidOccupantAt(destinationCell)`를 본다.
- 목적지에 `Unit`만 있다면 이동 실패가 아니라 정상 이동으로 처리한다.
- `Push`, `Flip`, `Item`은 `Box` 전용 질의로 바꾼다.
- `PlayerControlQueries.TryResolvePushContact`와 `TryResolveFlipTarget`은 stacked unit이 아닌 box만 대상으로 찾도록 바꾼다.

주의점

- 지금 구조에서는 `TryGetUnitAt`가 box 탐지에도 쓰이고 있어 의미 오염이 크다.
- 이 단계에서 그 의존을 정리하지 않으면 이후 버그가 반복된다.

완료 기준

- 플레이어가 적이 서 있는 칸으로 정상 이동 가능하다.
- 플레이어의 push/flip는 stacked unit에 반응하지 않고 box에만 반응한다.

주요 구현 내용

- `MovementExpander.ExpandMoveLike`의 목적지 해석을 `box/solid occupant` 우선으로 명시해 `Unit`만 있는 칸은 이동 차단으로 보지 않고 정상 `Move` group을 생성하도록 정리했다.
- 같은 경로에서 `Push` 입력은 `Box`가 없는 경우 즉시 `PushTargetNotBox`로 거절되게 했고, `Wall(EntityType.None)`/`Unit`을 box 상호작용 대상으로 오인하지 않도록 solid-layer 분기를 분리했다.
- `PlayerControlQueries.TryResolvePushContact` / `TryResolveFlipTarget`는 box 전용 helper를 통해 `TryGetBoxAt(...)`만 사용하도록 고정해 adjacent unit이 push hold나 flip action 시작 조건으로 섞이지 않게 했다.
- `MovementPhaseScenarioTests`, `PlayerMovementInputTests`, `TickReplayDeterminismTests`를 갱신해 scripted move/player move의 same-cell stacking, push/flip의 unit 비반응, replay determinism을 검증했다.

### 5단계. 이동 Resolver 충돌 규칙 수정 [완료]

목표

- 복수 Unit이 같은 타일로 들어오는 것을 허용한다.
- 단, box 이동, topology 이동, 특수 그룹은 계속 충돌 검사를 받는다.

대상 파일

- [MovementResolver.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Resolution/MovementResolver.cs)

작업

- `reservedDestinations`를 모든 group에 일괄 적용하지 않는다.
- 아래 조건의 move group은 동일 destination 허용 대상으로 분류한다.
- `GroupKind == Move`
- `TopologyChanges.Count == 0`
- `Moves` 대상이 모두 `EntityType.Unit`
- 위 경우 destination reservation과 edge reservation을 생략한다.
- `Push`, `Flip`, `Item`, `ForwardCellImpact`, topology transition 포함 group은 기존처럼 보수적으로 충돌 처리한다.

주의점

- 같은 타일 공유를 허용한다고 해서 box와 unit의 교차 이동까지 무제한 허용하면 안 된다.
- resolver는 `모든 이동 완화`가 아니라 `unit-only move 완화`로 제한해야 한다.

완료 기준

- 두 적이 같은 tick에 같은 destination으로 들어가는 시나리오가 통과한다.
- box move와 topology move의 기존 충돌 보호는 유지된다.

주요 구현 내용

- `MovementResolver`에 snapshot 기반 `unit-only move` 판별을 추가해 `GroupKind == Move`, `TopologyChanges.Count == 0`, `Moves` 대상이 모두 `EntityType.Unit`인 후보만 shared-destination 완화 대상으로 분리했다.
- destination/edge reservation을 `전체 충돌용`과 `shared-unit 차단용`으로 이원화해 unit-only move끼리는 같은 칸/edge를 공유할 수 있게 하면서도 `Push`/`Item`/topology change 같은 보수적 group은 unit move와 계속 상호 충돌하도록 유지했다.
- `TickPipeline`과 movement phase 테스트 helper가 resolver에 pre-movement snapshot을 넘기도록 연결했고, `MovementPhaseScenarioTests`와 `TickPipelineStageOneTests`를 확장해 same-destination unit stacking과 box/unit reservation 보호를 함께 검증했다.

### 6단계. 공격/접촉 규칙 수정 [완료]

목표

- 동일 타일 중첩이 실제 전투 규칙으로 동작하게 만든다.
- `공격 없이 충돌 자체로 피해`를 주는 적의 설계를 가능하게 만든다.

대상 파일

- [AttackExpander.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_Attack/Runtime/Expansion/AttackExpander.cs)
- [EnemyCombatPolicy.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyCombatPolicy.cs)
- [EnemyActionStateLogic.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyActionStateLogic.cs)
- 충돌 피해를 주는 적의 개별 로직 파일

작업

- melee attack 유효 조건을 `직교 인접`에서 `동일 타일 또는 직교 인접`으로 변경한다.
- facing 계산은 동일 타일일 때 기존 facing 유지로 처리한다.
- 충돌 피해 적은 `player와 same-cell일 때 피해 발생` 규칙을 명시한다.
- same-cell 접촉 피해는 tick 내 중복 발생 조건을 별도 정의한다.
- 추천 규칙은 `한 tick당 source-target pair당 1회`.

주의점

- 이 단계가 빠지면 `겹칠 수는 있는데 아무 일도 안 일어나는` 상태가 된다.
- 사용자 요구사항상 이 단계는 선택이 아니라 필수다.

완료 기준

- 플레이어와 적이 같은 타일에 존재할 수 있다.
- same-cell 상태에서 melee 또는 접촉 피해 규칙이 정상 발동한다.

주요 구현 내용

- `AttackExpander`의 direct attack contact check를 `same-cell 또는 직교 인접`으로 확장해 stacked unit 상태에서도 melee intent가 reject되지 않게 했다.
- `EnemyActionStateTargeting.ResolveFacing(...)`의 zero-delta 경로를 명시적으로 유지해 same-cell 공격 시작 시 기존 facing을 그대로 사용하도록 고정했다.
- `EnemyCombatPolicy`와 `EnemyAiConfig`에 `ContactSameCellAttackDecisionStrategy` / `AttackDecisionStrategyKind.ContactSameCell`을 추가해 `same-cell일 때만 피해를 주는` contact-damage enemy를 프로필로 authoring할 수 있게 했다.
- canonical test/profile factory 기반 contact-damage profile 경로를 추가했고, 이 경로는 `AttackState`의 same-tick execute를 그대로 재사용해 이동 후 overlap이 생긴 tick에 즉시 피해를 만들 수 있게 했다.
- same-cell contact damage의 tick 내 중복은 현재 구조에서 `entity당 attack phase ownership 1개 + source logic이 tick당 raw attack intent 1회 생성` 규칙으로 제한되며, scenario test에서 `DamageCommitted`가 tick당 1회만 발생하는지 고정했다.
- `AttackPhaseScenarioTests`, `EnemyAiScenarioTests`, `EnemyLogicTests`를 확장해 post-move same-cell melee, contact-damage enemy의 same-tick overlap damage, same-cell facing 유지, `ContactSameCell` range rule을 검증했다.

### 7단계. RemovedEntity impact target 결정론화 [완료]

목표

- stacked unit 위로 removed entity가 진입할 때 타깃이 항상 동일하게 선택되게 한다.

대상 파일

- [MovementExpander.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs)
- [MovementCommitter.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Commit/MovementCommitter.cs)

작업

- `ForwardCellImpact` 생성 시 cell만 기록하지 말고 실제 target entityId를 확정한다.
- 우선순위 규칙을 고정한다.
- `hostile unit 우선`
- 다수면 `entityId 오름차순`
- 필요 시 추후 `state priority`를 추가할 수 있으나 1차는 단순 규칙으로 간다.
- commit 단계는 다시 cell을 조회하지 말고 group 또는 reservation에 담긴 target을 그대로 사용한다.

완료 기준

- 같은 replay를 여러 번 돌려도 stacked cell impact 결과가 동일하다.

주요 구현 내용

- `SnapshotReadQueries.TryPickImpactTargetAt`를 `hostile unit 우선 + entityId 오름차순`으로 명시 계산하도록 보강해 stacked unit 컬렉션의 순회 순서에 암묵적으로 기대지 않게 했다.
- `MovementExpander`의 `ForwardCellImpact` candidate는 expand 시점에 선택된 실제 `target entityId`를 `ActionGroup.ForwardCellImpactTargetId`로 고정 기록하도록 바꿨다.
- `MovementCommitter`는 removed entity impact reservation 생성 시 더 이상 destination cell이나 move intent를 재조회하지 않고, group에 저장된 확정 target만 사용하도록 변경했다.
- `TickTraceFormatter`는 removed entity impact group의 `ImpactTarget`을 trace에 남기도록 보강해 replay/debug 시 target 결정 경로를 바로 확인할 수 있게 했다.
- `MovementPhaseScenarioTests`를 확장해 stacked hostile target 선택, expand된 group의 target 고정, commit 단계의 `intent lookup / cell 재조회 없음`을 검증했고, build 검증에서 `Game.Feature.Gameplay.Tests.csproj` 컴파일이 오류 없이 통과했다.

### 8단계. 프레젠테이션 오프셋 도입 [완료]

목표

- 같은 타일의 여러 Unit이 화면상 완전히 겹치지 않게 한다.

대상 파일

- [GameplayTickPresentationCoordinator.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs)
- [MotionTrack.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_Host/Runtime/MotionTrack.cs)
- 필요 시 presentation state store 관련 파일

작업

- `StoreCommittedEntityTargets`에서 presentable unit들을 cell별로 그룹화한다.
- 같은 cell의 unit들에게 slot index를 부여한다.
- slot pattern 제안
- 1명: center
- 2명: 좌우
- 3명: 삼각
- 4명: 사각
- 5명 이상: 작은 원형 분산
- 오프셋은 world up이 아니라 타일 면의 local tangent plane 기준으로 준다.
- box, removed entity, wall은 center 유지한다.

주의점

- 이 단계 없이 로직만 바꾸면 same-cell unit이 한 개체처럼 보인다.
- 플레이 감각상 이는 심각한 정보 손실이다.

완료 기준

- 플레이어와 적이 같은 타일에 있어도 둘 다 시각적으로 식별 가능하다.

주요 구현 내용

- `GameplayTickPresentationCoordinator.StoreCommittedEntityTargets(...)`에서 presentable entity를 먼저 수집한 뒤, `Unit`만 `cell -> entityId list`로 그룹화하고 `entityId 오름차순`으로 slot index를 확정하도록 변경했다.
- slot pattern은 `1명 center`, `2명 좌우`, `3명 삼각`, `4명 사각`, `5명 이상 원형 분산`으로 구현했고, `Box`/`RemovedEntity`/`Wall(None)`은 기존처럼 중심 pose를 유지한다.
- slot offset은 `ProjectedCellPose.LocalRotation`의 `right/up` 축을 사용해 타일 면의 local tangent plane 위에서만 적용되도록 구현해, floor/ceiling/front face 어디에서도 normal 방향으로 밀리지 않게 했다.
- 기존 motion start/end pose는 committed local target pose를 그대로 재사용하게 두어, same-cell 이동 후 도착 unit과 기존 occupant가 같은 tick 프레젠테이션에서 서로 다른 slot으로 정렬되도록 맞췄다.
- `GameplayTickPresentationCoordinatorTests`에 초기 same-cell 배치, 이동 후 same-cell 재정렬, ceiling face tangent-plane 보장을 검증하는 테스트를 추가했다.

현재 상태 메모

- 2026-04-07 기준으로 runtime에서는 same-cell unit presentation offset을 비활성화했다.
- 이유는 겹침이 발생하는 순간 기존 occupant까지 즉시 재배치되어 시각적으로 부자연스럽게 보였기 때문이다.
- overlap 규칙 자체는 유지되며, 현재는 stacked unit도 모두 타일 중심 pose를 공유한다.

### 9단계. 디버그, trace, determinism 보강 [완료]

목표

- 새 점유 모델이 디버깅 가능하고 재현 가능해야 한다.

대상 파일

- [TickTraceFormatter.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_Debug/Runtime/TickTraceFormatter.cs)
- [DeterminismHashBuilder.cs](/mnt/c/Users/user/2026TeamProject_J2M/Assets/_Features/Gameplay/Gameplay_Loop/Runtime/DeterminismHashBuilder.cs)
- fuzz/replay 관련 테스트 도구

작업

- occupancy dump는 같은 cell에 여러 line을 출력한다.
- 정렬 기준은 `face -> x -> y -> entityId`.
- determinism hash에 stacked unit 정보가 누락되지 않게 한다.

완료 기준

- replay artifact와 determinism hash가 stacked unit 상태를 안정적으로 반영한다.

주요 구현 내용

- `WorldSnapshot`/`SnapshotReadQueries`에 `stacked unit occupancy`와 `solid occupancy`의 ordered enumeration을 분리해, trace/hash가 새 점유 모델을 레이어별로 직접 읽게 했다.
- `TickTraceFormatter`의 occupancy dump는 이제 `Layer=Solid|Unit|RemovedEntity` 형식으로 출력되며, 전체 라인은 `face -> x -> y -> entityId` 순으로 재정렬되어 same-cell stacked state가 여러 줄로 안정적으로 노출된다.
- `DeterminismHashBuilder`는 기존 뭉뚱그린 non-removed entity occupancy 대신 `SolidOccupancy`, `StackedUnitOccupancy`, `RemovedEntityOccupancy`를 각각 canonical dump에 포함하도록 바꿔 stacked unit 정보와 solid layer 상태가 hash에 명시적으로 반영되게 했다.
- `TickReplayDeterminismTests`, `TickPipelineStageOneTests`, `FuzzDeterminismTests`를 갱신해 stacked unit/solid/removed entity layered occupancy dump, entityId 오름차순 same-cell ordering, replay artifact 포맷을 고정했다.

### 10단계. 테스트 추가 및 회귀 검증 [완료]

목표

- 새 규칙을 고정하고 회귀를 막는다.

필수 테스트 시나리오

- player가 enemy가 있는 칸으로 이동 가능
- enemy가 player가 있는 칸으로 이동 가능
- enemy 두 명이 같은 칸으로 동시에 이동 가능
- same-cell player/enemy에서 melee attack 가능
- same-cell 충돌 피해 적이 피해를 정상 적용
- same-cell unit 위로 removed entity가 들어갈 때 결정론적 타깃 선택
- stacked unit이 있어도 box push/flip 규칙은 유지
- stacked unit 위로 box가 이동하거나 착지하지 못함
- presentation slot offset이 적용되어 view가 겹치지 않음

대상 테스트 파일

- `Gameplay_Tests/EditMode/Scenario`
- `Gameplay_Tests/EditMode/Unit`
- 필요 시 `PlayMode` presentation 테스트

완료 기준

- 신규 핵심 테스트 통과
- 기존 movement/attack/cleanup/determinism 테스트 회귀 없음

주요 구현 내용

- `MovementPhaseScenarioTests`에 `Movement_EnemyMoveIntoPlayerCell_SucceedsAndStacks`를 추가해 `enemy -> player same-cell` 이동 허용을 문서 시나리오 이름 그대로 고정했다.
- `AttackPhaseScenarioTests`에 `Attack_AlreadySameCellContactAttack_SucceedsWithoutMovement`를 추가해 이미 같은 타일을 공유 중인 상태에서도 melee/contact attack이 정상 확장/커밋되는 경로를 고정했다.
- `EnemyAiScenarioTests`에 `EnemyAi_ContactDamageProfile_AlreadySharingPlayerCell_DealsDamageWithoutMoving`를 추가해 contact-damage enemy가 same-cell 상태에서 이동 없이 피해를 적용하는 규칙을 회귀 테스트로 잠갔다.
- `GameplayWorldStateTestFactory.cs`의 test support에 `GameplayCliTestRunner.RunEditModeTests()`를 추가해 Unity batchmode에서 선택한 EditMode 테스트 집합을 동기 실행하고 요약을 로그로 남길 수 있게 했다.
- headless regression subset으로 movement / attack / cleanup / determinism / presentation 관련 `16`개 overlap 핵심 테스트를 실행했고 `Passed=16`, `Failed=0`, `Skipped=0`을 확인했다.

## 구현 순서 권장

권장 순서는 다음과 같다.

1. 1단계 월드 점유 모델 분리
2. 2단계 snapshot 질의 API 분리
3. 3단계 배치 차단 규칙 재정의
4. 4단계 이동 확장 수정
5. 5단계 movement resolver 수정
6. 6단계 공격/접촉 규칙 수정
7. 7단계 removed entity target 결정론화
8. 9단계 debug/hash 보강
9. 10단계 테스트 추가
10. 8단계 presentation 오프셋 마감

설명

- presentation은 중요하지만, 로직 불변식이 먼저 고정되어야 한다.
- 그렇지 않으면 화면 오프셋 설계를 두 번 이상 갈아엎게 된다.

## 리스크

- `TryGetUnitAt`의 의미가 남아 있는 레거시 호출부를 놓치면 런타임 버그가 생길 수 있다.
- removed entity target 선택을 commit 단계까지 미루면 stacked unit 환경에서 비결정성이 생길 수 있다.
- same-cell melee를 허용했는데 contact damage 중복 규칙을 정하지 않으면 과도한 피해가 날 수 있다.
- presentation 오프셋이 없다면 실제 플레이에서는 겹침 규칙이 읽히지 않는다.

## 수용 기준

아래 조건을 모두 만족해야 이번 작업을 완료로 본다.

- 플레이어와 적이 같은 타일에 동시에 존재 가능
- 적과 적이 같은 타일에 동시에 존재 가능
- 같은 타일 중첩이 단순 시각 상태가 아니라 실제 공격/접촉 판정 상태로 동작
- box 퍼즐 규칙은 유지
- removed entity target 선택은 결정론적
- debug/hash/test 기반 회귀 검증 완료

## 구현 메모

- 이번 작업의 본질은 `occupancy 완화`가 아니라 `Unit 접촉 상태를 공식 게임 규칙으로 도입`하는 것이다.
- 따라서 성공 조건은 `적이 길막 없이 온다`가 아니라 `플레이어-적 same-cell 상황이 이동, 공격, 접촉피해, 연출에서 모두 자연스럽게 동작한다`이다.
