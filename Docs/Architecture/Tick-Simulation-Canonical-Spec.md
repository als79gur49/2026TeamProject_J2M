# Tick Simulation Canonical Spec

이 문서는 현재 `Assets/_Features/Gameplay` 구현과 맞는 canonical architecture spec이다. gameplay flow, authoritative state boundary, presentation contract의 기준 문서로 사용한다.

## Code Sources
- Tick pipeline and result: `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs`, `TickRunner.cs`, `TickResult.cs`, `TickResultBuilder.cs`, `TickPresentationData.cs`
- Gameplay phases and runtime stage enum: `Assets/_Features/Gameplay/Gameplay_Model/Runtime/Phases/TickPhase.cs`
- Authoritative state and snapshot queries: `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs`, `WorldSnapshot.cs`, `Queries/SnapshotReadQueries.cs`, `Queries/WorldPlacementPolicy.cs`
- Movement / Attack / Cleanup: `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/*`, `Gameplay_Attack/Runtime/*`, `Gameplay_Cleanup/Runtime/CleanupProcessor.cs`
- Presentation boundary: `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`

## Vocabulary Boundary
- Gameplay phase vocabulary:
  - `Movement`
  - `Attack`
  - `Cleanup`
  - `Respawn`
- Runtime stage vocabulary:
  - `Plan`
  - `Resolve`
  - `Finalize`
  - `Cleanup`
  - `Respawn`
- Canonical rule:
  - `Movement / Attack / Cleanup / Respawn`는 gameplay flow 설명에 사용한다.
  - `Plan / Resolve / Finalize / Cleanup / Respawn`는 pipeline execution stage 설명에만 사용한다.
  - `TickPhase`는 현재 코드에서 runtime stage enum이다. gameplay phase의 대표 명칭으로 승격하지 않는다.

## Core Invariants
- 시뮬레이션은 fixed tick deterministic model이다.
- `WorldState`만 authoritative write target이며, write는 commit path를 통해서만 일어난다.
- 계산 단계는 `WorldSnapshot`과 phase-local data만 읽는다.
- 각 gameplay phase 내부 구조는 `Intent -> Expand/Resolve -> Commit`이다.
- `TickResult -> TickPresentationData -> ViewPresenter`로 logic/view가 분리된다.
- Movement가 만든 `ImpactReservation`은 Attack이 소비한다.

## Query Layer
- Canonical query boundary는 다음 순서를 따른다.
  - `Storage Query`: raw occupancy, raw terrain, deterministic ordered enumeration
  - `Semantic Query`: `TryGetSolidSemanticAt(...)`, `IsWallAt(...)`, `IsBoxAt(...)`, `TryGetTerrain(...)`, `IsTerrainBlockedForUnit(...)`
  - `Semantic Convenience`: `TryGetPrimaryUnitAt(...)` 같은 representative-only helper
  - `Legality Query`: `Placement / Traversal / Settlement` verdict만 반환하는 판정 계층
  - `Resolver`: action-specific branch, fallback, target 선택
  - `Committer`: 이미 resolve된 payload만 authoritative state에 적용
- naming rule:
  - storage/semantic query는 명사형 질문만 가진다.
  - legality query는 allowed/blocked verdict만 가진다.
  - action 이름이 들어간 helper는 canonical semantic vocabulary에 포함하지 않는다.
  - gameplay core는 semantic helper를 file-local로 조합해 composite legality verdict를 재조립하지 않는다. actor/action이 들어간 allowed/blocked 판단은 legality owner에 둔다.

## Traverse / Settle Seam
- `Traverse`는 actor가 현재 snapshot과 topology transition requirement 위에서 next step을 통과할 수 있는지 묻는 runtime legality seam이다.
- `Settle`는 actor가 same-tick effects와 reservation status를 반영한 뒤 terminal cell에 끝날 수 있는지 묻는 runtime legality seam이다.
- canonical legality owners:
  - `RuntimePlacementValidityPolicy`: spawn/respawn/debug authoritative placement legality
  - `RuntimeTraversalLegalityPolicy`: traversal legality
  - `RuntimeSettlementLegalityPolicy`: settlement legality
- caller rule:
  - resolver와 `TickPipeline`은 candidate ordering, `TraverseContext`/`SettlementContext` 조립, typed evidence 전달만 수행한다.
  - resolver와 `TickPipeline`은 file-local helper로 legality 의미를 재조립하지 않는다.
- `TickPipeline`은 orchestration-only owner다. legality owner가 아니며 `SpatialState` source aggregation owner도 아니다.

## SpatialState Read Model
- `SpatialState`는 storage replacement가 아니라 internal canonical read-model axis다.
- authoritative occupancy storage는 계속 `WorldState` layered occupancy와 `boardPresence`에 남는다.
- current production runtime에서 legality/query code가 소비하는 state는 `Anchored`와 `Airborne`뿐이다.
- `Phased`와 `Attached`는 vocabulary slot을 고정하기 위한 reserved future state이며 current production runtime에서는 non-emittable이다.
- `SpatialStateResolver`는 query/state layer의 유일한 spatial aggregation owner다.
  - 읽는 source:
    - `EntityState.boardPresence`
    - `EnemyJumpRuntimeState`
    - `CubeTopologyState` face visibility
  - 읽지 않는 source:
    - reservation
    - modifier/blocker facts
    - caller-local booleans
    - action target selection results
- canonical resolver outputs:
  - `ResolvedSpatialState.Kind`
  - `ResolvedSpatialState.ClaimsAuthoritativeOccupancy`
  - `ResolvedSpatialState.IsGameplayVisible`
  - `ResolvedSpatialState.Source`
- canonical mapping:
  - `Occupying + jump none + active face -> Anchored`
  - `Occupying + jump none + inactive face -> AnchoredHiddenByTopology`
  - `Occupying + jump windup/cooldown -> Anchored`
  - `Detached + jump airborne -> Airborne`
  - `Detached + jump none -> Anchored` with no authoritative occupancy claim
  - `Detached + jump windup/cooldown` and `Occupying + jump airborne` are invalid source combinations
- forbidden rule:
  - `Detached == Airborne` 자동 승격 금지
  - caller별 `boardPresence + jumpState` ad-hoc switch 금지
  - inactive-face hidden state를 `Airborne`로 근사 해석하는 shortcut 금지

## Legality Contexts
- base legality context는 core field budget을 유지한다.
  - `TraverseContext`
    - `Snapshot`
    - `Actor`
    - `OriginCell`
    - `CandidateCell`
    - `EvaluationTopology`
    - `TransitionRequirement`
    - `ReservationStatus`
  - `SettlementContext`
    - `OccupancySnapshot`
    - `Actor`
    - `TerminalCell`
    - `TerminalTopology`
    - `RequestedTerminalState`
    - `ReservationStatus`
- feature-specific semantics는 base context를 늘리지 않고 typed evidence로 전달한다.
  - `JumpLandingEvidence`
  - `ImpactFollowThroughEvidence`
- context rule:
  - raw blocker list, raw occupant enumeration, semantic fact cache, mutation handle, caller-specific boolean은 base context에 넣지 않는다.
  - `LegalityActorRef`는 raw `boardPresence`나 raw jump phase가 아니라 `ResolvedSpatialState`만 운반한다.

## Boundary Inventory
- canonical boundary-case inventory는 다음 ID로 유지한다.
  - `TS-01`: traverse allowed / settle allowed
  - `TS-02`: traverse allowed / settle blocked
  - `TS-03`: traverse blocked / settle not asked
  - `TS-04`: traverse allowed with topology transition / settle allowed
  - `TS-05`: traverse allowed with topology transition / settle blocked
  - `TS-06`: traverse allowed / settle blocked due to reservation conflict
  - `TS-07`: traverse allowed / settle blocked due to accepted-destroy/vacate evidence absence
  - `TS-08`: airborne actor / anchored blocker interaction
  - `TS-09`: anchored actor / airborne blocker interaction
- tests와 docs는 같은 ID를 공유한다. 새 traversal/settlement legality case를 추가할 때는 inventory row와 test coverage를 함께 갱신한다.

## Stage Contract
- `Plan`과 `Resolve`는 phase-entry snapshot과 published reservation read model만 읽는다.
- `Finalize`만 `WorldState`를 mutate할 수 있다.
- `Finalize`는 legality를 재평가하거나 target을 다시 고르지 않는다.
- semantic slice handoff는 오직 두 가지다.
  - 이전 slice `Finalize` 이후의 새 snapshot
  - 이전 slice가 publish한 finalized reservation output

## Occupancy And Queries
- 현재 authoritative occupancy storage는 `WorldState`의 세 레이어다.
  - `_stackedUnitsByCell`
  - `_solidOccupancy`
  - `_projectileOccupancy`
- terrain canonical storage는 `TerrainData`의 `SurfaceCell -> TerrainCellState`다.
- Canonical query vocabulary는 `WorldSnapshot`의 layered API를 기준으로 한다.
  - `EnumerateUnitsAt(...)`
  - `TryGetSolidSemanticAt(...)`
  - `IsWallAt(...)`
  - `IsBoxAt(...)`
  - `TryGetTerrain(...)`
  - `IsTerrainBlockedForUnit(...)`
  - `TryPickImpactTargetAt(...)`
  - `TryGetUnitTraversalBlocker(...)`
- Legacy compatibility API는 canonical vocabulary가 아니다.
  - `TryGetUnitAt(...)`
  - `TryGetSolidOccupantAt(...)`
  - `IsBlockedForUnit(...)`
  - `BlocksMovement(...)`
- `TryGetBoxAt(...)`, `CreateDefaultQueryCell(...)`, `SurfaceCell.FromPlanar(...)`, terrain `Vector2Int` overload는 compatibility helper다. 새 gameplay core path는 사용하지 않는다.
- `TryGetPrimaryUnitAt(...)`는 helper/convenience API로만 취급한다. stacked-unit 모델의 대표 vocabulary로 쓰지 않으며, gameplay core에서는 post-legality 대표값 조회 외에 승격하지 않는다.
- query interpretation rule:
  - occupancy truth와 `SpatialState` truth는 다르다.
  - `ClaimsAuthoritativeOccupancy`는 storage truth에 대한 read fact다.
  - gameplay traversal blocking, settlement blocking, target selection 참여 여부는 `SpatialState` semantics table이 해석한다.

## Layer Rules
- Unit layer는 stacked 허용이다.
- Solid layer는 `Box`와 `Wall`이 점유한다.
- Projectile layer는 unit과 분리된다.
- Canonical rule set:
  - `Unit + Unit` 허용
  - `Unit + Box/Wall` 금지
  - `Box + Box` 금지
  - `Projectile + Unit` 허용

## Validity Taxonomy
- `RepresentableState`: storage invariant를 깨지 않는 상태
- `RuntimeReachableState`: commit path가 실제로 만들 수 있는 상태
- `AuthorableInitialState`: stage authoring이 허용하는 초기 상태
- `DebugSpawnableState`: representable이지만 runtime reachable은 아닐 수 있는 debug/test 주입 상태
- 이번 단계 구현:
  - `RuntimePlacementValidityPolicy`
  - `AuthoringStageValidityPolicy`
  - `DebugSpawnValidityPolicy`
- `ImportableState / ReplayableState / SaveableState`는 taxonomy에 포함되지만 이번 단계에서는 문서 contract만 가진다.

## Gameplay Flow
- 상위 흐름은 `Input -> TickPipeline -> Movement -> Attack -> Cleanup -> Respawn`이다.
- Movement:
  - raw movement intent를 수집한다.
  - execute tick에 push/flip을 재판정한다.
  - 성공 시 movement commit을 수행한다.
  - impact 실패 시 box는 이동하지 않고 `ImpactReservation`만 생성한다.
- Attack:
  - raw attack input, `ImpactReservation`, delayed effect handoff를 소비한다.
  - reservation을 attack damage로 전개한다.
- Cleanup:
  - `hp <= 0` 또는 `markedForDeath` removal을 확정한다.
- Respawn:
  - cleanup 이후 respawn eligibility를 반영한다.

## Action Runtime
- player authoritative action runtime은 `Gameplay_PlayerControl/Runtime/PlayerControlState.cs`의 `PlayerActionRuntimeState`다.
- Canonical action timeline vocabulary:
  - `windup`
  - `execute`
  - `recovery`
- `PreMovementState`는 historical/runtime-internal 이름으로만 취급한다. canonical public vocabulary가 아니다.

## Attack IR Boundary
- `Intent` concept은 유지한다.
- 그러나 다음은 phase-private normalized IR이다.
  - concrete `AttackIntent`
  - `AttackInputKind`
  - synthetic `intentId` assignment
  - `groupId`-centric metadata
- `ImpactReservation`은 external public contract가 아니라 gameplay module 내부 inter-phase contract로 취급한다.

## Presentation Contract
- View는 tick 중간 mutable state를 직접 읽지 않는다.
- Presenter가 소비하는 contract는 `TickResult.PresentationData`다.
- host/view code는 `CleanupPhaseResult` 같은 phase-private result에 직접 결합하지 않는다.

## Cleanup Policy
- `hp 0` / `markedForDeath` entity는 Cleanup 전까지 authoritative world에 남아 있을 수 있다.
- 이 상태는 placement/blocking과 impact-target selection에서 동일한 의미를 갖지 않는다.
- canonical query 표현은 legacy `IsBlockedForUnit` 대신 layered query와 `TryGetUnitTraversalBlocker`로 설명한다.
