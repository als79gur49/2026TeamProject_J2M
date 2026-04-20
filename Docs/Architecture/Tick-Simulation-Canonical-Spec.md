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
- Push is current runtime follow-through formalization.
- Flip is impact-result-dependent action uplift.
- `ImpactDisposition` is a narrow internal Push/Flip-only contract, not a generalized impact framework.
- broader combat/movement framework generalization is a non-goal.

## Query Layer
- Canonical query boundary는 다음 순서를 따른다.
  - `Storage Query`: raw occupancy, raw terrain, deterministic ordered enumeration
  - `Semantic Query`: `TryGetSolidSemanticAt(...)`, `IsWallAt(...)`, `IsBoxAt(...)`, `TryGetTerrain(...)`, `IsTerrainBlockedForUnit(...)`
  - `State Query`: `ResolvedSpatialState` base fact, occupancy claim, gameplay visibility, precompiled actor capability fact
  - `Modifier Query`: legality domain/evidence 기준 override만 제공하는 narrow read seam
  - `Reservation Query`: frozen reservation export를 legality read seam으로 번역하는 adapter
  - `Semantic Convenience`: `TryGetPrimaryUnitAt(...)` 같은 representative-only helper
  - `Legality Query`: `Placement / Traversal / Settlement` verdict만 반환하는 판정 계층
  - `Resolver`: action-specific branch, fallback, target 선택
  - `Committer`: 이미 resolve된 payload만 authoritative state에 적용
- naming rule:
  - storage/semantic query는 명사형 질문만 가진다.
  - legality query는 allowed/blocked verdict만 가진다.
  - `StateQuery`는 base fact만, `ModifierQuery`는 override만 제공한다.
  - action 이름이 들어간 helper는 canonical semantic vocabulary에 포함하지 않는다.
  - gameplay core는 semantic helper를 file-local로 조합해 composite legality verdict를 재조립하지 않는다. actor/action이 들어간 allowed/blocked 판단은 legality owner에 둔다.

## Traverse / Settle Seam
- `Traverse`는 actor가 현재 snapshot과 topology transition requirement 위에서 next step을 통과할 수 있는지 묻는 runtime legality seam이다.
- `Settle`는 actor가 same-tick effects와 reservation status를 반영한 뒤 terminal cell에 끝날 수 있는지 묻는 runtime legality seam이다.
- `Attached`는 이번 단계에서도 reserved future state다.
- canonical legality owners:
  - `RuntimePlacementValidityPolicy`: spawn/respawn/debug authoritative placement legality
  - `RuntimeTraversalLegalityPolicy`: traversal legality
  - `RuntimeSettlementLegalityPolicy`: settlement legality
- caller rule:
  - resolver와 `TickPipeline`은 candidate ordering, `TraverseContext`/`SettlementContext` 조립, typed evidence 전달만 수행한다.
  - resolver와 `TickPipeline`은 file-local helper로 legality 의미를 재조립하지 않는다.
- `TickPipeline`은 orchestration-only owner다. legality owner가 아니며 `SpatialState` source aggregation owner도 아니다.

## Blocker Vocabulary
- current canonical blocker kind는 정확히 다섯 개다.
  - `BoardEdge`
  - `Terrain`
  - `Solid`
  - `Unit`
  - `Reservation`
- extension rule:
  - 새 top-level blocker kind는 현재 다섯 source 어디에도 속하지 않는 새 world-source가 실제로 생길 때만 허용한다.
  - 기존 source 상세화는 top-level kind를 늘리지 않고 sub-facet으로만 확장한다.
- future slot reservation:
  - `host/socket/attachment`: 실제 host relation이 독립 blocker source가 될 때만 새 top-level kind 검토
  - `field/aura`: terrain/entity/reservation이 아닌 독립 field source가 생길 때만 새 top-level kind 검토
  - `targetability-only suppression`: blocker vocabulary가 아니라 `ModifierQuery` 축으로 유지
  - `reservation detail`: `Reservation` top-level kind 유지, future `cell/edge/entity/payload/topology-exclusive` facet은 `ReservationQuery`/central factory에서만 확장
- governance rule:
  - 새 blocker kind 또는 facet 추가 시 갱신 위치는 `canonical spec`, `central blocker factory`, `blocker truth tests`, `central diagnostics formatter`로 제한한다.

## SpatialState Read Model
- `SpatialState`는 storage replacement가 아니라 internal canonical read-model axis다.
- authoritative occupancy storage는 계속 `WorldState` layered occupancy와 `boardPresence`에 남는다.
- current live producer가 emit하는 state는 `Anchored`, `Airborne`, `Phased`다.
- `Phased`는 `WorldState` authoritative carrier를 가진 live runtime state이며 current live owner lane은 internal `PreMovementState` write-path다.
- current production gameplay concrete live source는 `PlayerControlStateLogic`의 player flip windup과 `EnemyLogic`의 enemy locked-target cross-through validator다.
- current wall-pass enemy는 baseline validator consumer다. seam proof용 minimal vertical slice이며 generalized phase movement template가 아니다.
- locked-target dependency is current validator-local dependency, not generic phase dependency.
- same-face / straight-line / target-behind +1 / single-terminal chooser are local geometry rules, not reusable phase template.
- horizontal expansion validation을 위한 internal validation owner는 `SystemPreMovementValidationLogic` 하나만 추가로 허용한다. 이것은 internal validation owner, not public scripted framework다.
- `Attached`는 consumer/producers 모두 closed 상태를 유지한다.
- `SpatialStateResolver`는 query/state layer의 유일한 spatial aggregation owner다.
  - 읽는 source:
    - `EntityState.boardPresence`
    - `EnemyJumpRuntimeState`
    - `PhasedRuntimeState`
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
- ownership rule:
  - `ResolvedSpatialState`는 base fact owner다.
  - `SpatialStateSemantics`는 default participation table owner다.
  - `ModifierQuery`는 domain/evidence-specific override owner다.
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
  - caller-local bool / action-state 조합으로 `Phased`를 추론하는 것 금지
  - `PhasedRuntimeState`와 `EnemyJumpRuntimeState` active coexistence 금지
- timing rule:
  - pre-movement `Phased` enter의 earliest observable point는 `snapshotAfterEnemyAi`가 아니라 pre-movement batch가 projected world에 적용된 뒤의 `planSnapshot`이다.
  - sibling pre-movement logic는 모두 같은 input snapshot만 본다. earlier/later sibling이 same-pass `SetPhasedState`를 서로 관측하지 못한다.
  - 같은 tick movement legality, movement expansion, fresh target acquisition은 `planSnapshot` truth만 본다.
  - `postMovementSnapshot`과 pre-attack `attackSnapshot`은 clear가 apply되기 전까지 active phased carrier를 유지해서 본다.
  - later resolve/cleanup cancel은 cancel 이후에 생성된 snapshot부터만 관측된다. earlier snapshot을 retroactive하게 다시 쓰지 않는다.
  - incompatible exclusive state enter는 explicit clear ordering이 먼저 와야 한다. current v1에서 active jump enter는 `SetEnemyJumpState(active)`보다 앞선 `SetPhasedState(Clear)`를 요구한다.
  - final replay/hash는 final snapshot carrier만 canonical input으로 사용한다.
- source extension rule:
  - 새 phase source는 `PhasedRuntimeStateOwnerKind`, emitting stage, enter/sustain/exit/forced-cancel rule, earliest observable snapshot을 함께 정의해야 한다.
  - 새 source는 carrier truth-source를 늘리지 않고 `IPhasedStateCommitContext.SetPhasedState(...)` 경유 write만 추가할 수 있다.
  - current horizontal validation owner는 `PreMovementState`, `PlanSnapshot`, `FreshSelectionSuppressed`, `AnchoredLikeDefault`, `ReservationRead=None`으로 잠근다.
  - source path는 reservation을 읽지 않는다. `CommitPreMovementState(...)`에서 `GetCellStatus(...)`, `GetEdgeStatus(...)`, `GetEntityStatus(...)`, `FrozenMovementReservationExport` 직접 참조는 out-of-scope다.
  - 다음 단계 implementer는 `EnemyPhaseRelocation`을 generalized phase action schema로, `EnemyPreMovement`를 default future owner precedent로, current chooser를 phase 일반 원리로 가정하면 안 된다.
  - 새 source를 추가할 때는 owner enum, timing row, coexistence matrix, trace/hash dump, allowlist test, source metadata coverage를 같이 갱신해야 한다.

## Participation Axes
- `occupancy claim`은 authoritative storage/read fact다.
- `gameplay visibility`는 gameplay query visibility fact다.
- `traversal blocking`, `settlement blocking`, `targetability participation`은 legality/query interpretation axis다.
- canonical rule:
  - occupancy claim과 visibility는 legality verdict를 직접 만들지 않는다.
  - `SpatialStateSemantics` default + `ModifierQuery` override가 participation axis를 결정한다.
  - `SpatialState.Kind == Phased`로 occupancy claim을 추론하지 않는다. consumer는 반드시 `ClaimsAuthoritativeOccupancy` fact를 읽는다.
  - `Phased`의 future semantic envelope 전체는 아직 고정하지 않는다.
  - current live profile은 traversal `Unit/Solid` bypass, fresh target suppression, anchored-like settlement default만 고정한다.
  - current wall-pass enemy는 baseline validator consumer다. seam proof용 minimal vertical slice이며 generalized phase movement template가 아니다.
  - current v1 profile은 `ClaimsAuthoritativeOccupancy=true`와 active-face visibility를 current implementation default로 사용하지만, 이것을 future non-claim/overlap model의 구조 원칙으로 승격하지 않는다.
  - current targetability suppression은 base spatial default다. current enemy lock retention is `EnemyActionStateTargeting` current lock path 전용 narrow hook다. future `impact-only suppression`, `detection-only suppression`, broader source-specific overrides는 `ModifierQuery` typed-evidence hook로만 연다.
  - `FreshSelectionSuppressedWithCurrentEnemyLockRetention`는 current stage-local contract 이름일 뿐이며, future lock taxonomy의 generic seed가 아니다.

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
- phased ordering contract:
  - projected snapshot은 `base snapshot + ordered finalization ops replay`의 canonical preview다.
  - authoritative final world는 같은 ordered ops를 `WorldState`에 apply한 결과와 observationally 동일해야 한다.
  - `FrozenMovementReservationExport`는 reservation book만 export한다. `PhasedRuntimeState`는 legality/query input일 수는 있지만 reservation export payload의 field가 되지 않는다.
- phased write-path minimality:
  - current production gameplay live writer는 `PlayerControlStateLogic`와 `EnemyLogic` 둘뿐이다.
  - 추가 non-test validation writer는 `SystemPreMovementValidationLogic` 하나만 허용한다. 이것은 internal validation owner이며 public authoring/debug API가 아니다.
  - 허용 seam은 `WorldState`/`WorldSnapshot` carrier path, `IPhasedStateCommitContext`, `FinalizationBatch`/`ProjectedWorld`, trace/hash/docs/tests까지만이다.
  - public query/command/authoring/debug API, reservation export schema, serialized/import/save schema, finalize legality recomputation은 이번 단계에서 건드리지 않는다.
  - current wall-pass enemy가 성공해도 generalized phase movement, pathfinding expansion, terminal phase settle, non-claim occupancy의 evidence가 되지 않는다.

## Correlation Contract
- post-plan runtime/canonical carrier의 plan-level correlation key는 `ActionPlanId`다.
- `GroupId`는 `ActionGroup` compatibility IR vocabulary이며 canonical result carrier name이 아니다.
- canonical provenance field:
  - `DamageResolutionRecord.ActionPlanId`
  - `DestroyResolutionRecord.ActionPlanId`
  - `DelayedAttackEffectRecord.SourceActionPlanId`
  - `ResolutionRecord.ActionPlanId`
  - `FinalizationOperationMetadata.ActionPlanId`
- semantic field와 provenance metadata는 분리해서 해석한다.
  - semantic result 예: `SourceId`, `SourceKind`, `TargetId`, `Amount`, `Accepted`, `RejectReason`
  - provenance/correlation 예: `ActionPlanId`, `IntentId`, `LocalActionIndex`, `EffectSequence`
- `IntentId`는 deterministic ordering, resolver dedupe, payload/finalization metadata, diagnostics correlation에 남는 canonical internal ID다.
- `DamageResolutionRecord.GroupId`, `DestroyResolutionRecord.GroupId`, `DelayedAttackEffectRecord.SourceActionGroupId`는 migration compatibility alias이며 새 runtime reader가 직접 읽어서는 안 된다.
- structured trace의 `Plan=` / `SourcePlan=` token은 canonical structured trace surface다. machine-readable trace/debug/tooling은 typed runtime carrier 다음 우선순위로 이 표면을 읽는다.
- free-form `CommitEvents` / `EventLog`의 `G=` token은 compatibility token in free-form event log다. current `ActionPlanId` value를 mirror하지만 old semantic GroupId revival이 아니다.
- 새 parser/test/tooling은 `G=`를 canonical parser surface로 읽지 않고 `ActionPlanId` / `SourceActionPlanId` 또는 structured trace `Plan=` / `SourcePlan=`를 읽는다.
- `TickEntityMotion`은 committed logical movement between already-committed cells만 나타낸다. pre-impact anticipation이나 transient recoil을 authority carrier로 사용하지 않는다.
- `FlipImpactPresentationSignal`은 presentation-only carrier다. `WorldState`, damage/destroy resolution, movement/attack semantic, determinism hash input의 authority가 아니다.
- `Flip DestroySelf` / `Flip Stay`는 fake `TickEntityMotionKind.Flip`을 만들지 않는다. common pre-impact flip arc는 `TickResult.PresentationData.FlipImpactSignals`로 전달한다.
- `Flip FollowThrough`는 기존 `TickEntityMotionKind.Flip` 경로를 유지한다.
- `FlipImpactPresentationSignal.SourceActionPlanId`가 canonical join key다. 새 reader는 `GroupId` / `SourceActionGroupId`를 읽지 않는다.
- `Flip Stay`는 actual entity view override track을 사용하고, `Flip DestroySelf`는 transient clone/effect path를 사용한다.
- flip contact timing은 centralized timing setting에서 나오며 box contact, destroy break start, player release, stay recoil branch가 같은 contact normalized time을 공유한다.
- `Flip DestroySelf`에서 shared contact normalized time은 final impact-pose arrival time이 아니라 break/release onset threshold다. destroy transient root flight는 일반 flip duration 전체를 사용하고, break/fade는 그 threshold부터 overlap된다.

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
  - target selection은 gameplay query participation과 동일 축이 아니다. current production runtime에서는 같은 결과를 내더라도 canonical seam은 분리한다.

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
  - hostile `BoxImpact`는 ordinary movement commit이 아니라 `ImpactReservation` handoff로 보낸다.
  - Push impact는 target이 죽고 follow-through settlement가 허용되면 same-tick lethal follow-through를 commit한다.
  - Flip impact는 current Push/Flip impact-disposition plan에서 post-attack disposition을 resolve한다.
- Attack:
  - raw attack input, `ImpactReservation`, delayed effect handoff를 소비한다.
  - reservation을 attack damage로 전개한다.
  - resolve는 current Push/Flip impact-disposition plan에서 `Stay / FollowThrough / DestroySelf` 중 하나를 닫는다.
- Cleanup:
  - `hp <= 0` 또는 `markedForDeath` removal을 확정한다.
- Respawn:
  - cleanup 이후 respawn eligibility를 반영한다.

## Push / Flip Impact Handoff Note
- Push change is formalization, not a new framework.
- Do not redesign Push around a new disposition framework; formalize what runtime already does.
- Flip change is a narrow impact-result-dependent uplift.
- Flip is the only family that gains new outcome-dependent action behavior in this step.
- `Flip lethal but landing denied = Stay` is a current contract decision.
- Changing this row requires a new design decision; do not extend the current enum or tests as if this row were a permanent rule.
- Transient collision/break is presentation-only and must not be used as gameplay truth.

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

## Legality Diagnostics
- stable behavior-contract field:
  - `LegalityDomain`
  - `LegalityVerdict`
  - `ReservationStatus`
  - `LegalityBlockerKinds`
  - existing actor spatial summary fields
- provisional debug-only field:
  - capability flags
  - modifier flags
  - reservation sub-facet detail
  - future blocker origin/facet detail
- rule:
  - canonical replay/hash trace는 stable field만 의존한다.
  - provisional legality detail은 dedicated verbose diagnostics section으로만 추가한다.
  - provisional field는 `LegalityDiagVersion` 없이 stable trace surface에 넣지 않는다.

## Cleanup Policy
- `hp 0` / `markedForDeath` entity는 Cleanup 전까지 authoritative world에 남아 있을 수 있다.
- 이 상태는 placement/blocking과 impact-target selection에서 동일한 의미를 갖지 않는다.
- canonical query 표현은 legacy `IsBlockedForUnit` 대신 layered query와 `TryGetUnitTraversalBlocker`로 설명한다.
