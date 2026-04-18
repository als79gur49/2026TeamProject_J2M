# Gameplay Rules Appendix

이 문서는 canonical spec의 보조 문서다. 구조 vocabulary가 아니라 gameplay rule text를 기록한다.

## Push
- 관련 코드:
  - `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs`
  - `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Commit/MovementCommitter.cs`
- rule:
  - push execute tick에서 다음 칸이 비어 있으면 box를 이동 commit한다.
  - 다음 칸이 적 유닛이면 `impact`다.
  - `impact`에서는 box가 그 칸에 들어가지 않는다.
  - Movement는 `ImpactReservation`만 만들고, Attack이 same-tick damage를 적용한다.

## Flip
- 관련 코드:
  - `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs`
  - `Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlStateLogic.cs`
- rule:
  - flip execute tick에서 landing cell을 다시 판정한다.
  - landing cell이 적 유닛이면 `impact`다.
  - `impact`에서는 box가 source cell에 남는다.
  - landing cell이 wall, solid box, terrain, board edge면 `blocked`다.
  - `blocked`에서는 impact가 생기지 않는다.

## Traverse vs Settle
- `Traverse`는 actor가 이동 step 또는 topology transition을 통과할 수 있는지 묻는다.
- `Settle`는 통과 후 terminal cell에 끝날 수 있는지 묻는다.
- runtime legality owner는 다음처럼 분리한다.
  - `Placement`: spawn/respawn/debug authoritative placement legality
  - `Traversal`: movement step legality
  - `Settlement`: landing/follow-through legality
- 대표 distinction:
  - traverse는 allowed지만 settlement는 blocked일 수 있다.
  - traverse가 blocked면 settlement는 묻지 않는다.

## SpatialState
- current production runtime에서 legality/query consumer가 실제로 읽는 state는 `Anchored`와 `Airborne`뿐이다.
- `Airborne`는 jump owner가 만든 explicit non-anchored state일 때만 인정한다.
- `Phased`와 `Attached`는 reserved future state다. current production runtime behavior로 해석하지 않는다.
- `Anchored`는 기본 spatial mode다. `Detached`라고 해서 자동으로 `Airborne`가 되지 않는다.

## Airborne Jump Landing
- `Airborne` actor는 traversal 중에는 일반 occupancy blocker로 취급되지 않는다.
- jump landing settlement에서는 requested terminal state가 `Anchored`로 돌아오며 landing cell legality를 다시 판정한다.
- anchored blocker가 landing cell을 차지하고 있으면 settlement가 blocked일 수 있다.

## Common Execute Outcomes
- canonical public rule text는 `success / impact / blocked` 축을 사용한다.
- 이 축은 semantic summary다. internal resolver는 더 많은 payload를 가질 수 있다.
- implementation에서 유지해야 하는 payload 관심사:
  - `createdImpactReservation`
  - `boxRelocated`
  - `resolvedCell`
  - `enteredRecovery`
  - `rejectReason`

## Recovery
- push/flip의 `impact`와 `blocked`는 둘 다 execute attempted failure다.
- 둘 다 cancel이 아니라 recovery로 진입한다.
- view는 `TickPresentationData.PlayerActionSignals`만 보고 recovery를 표현한다.

## Cleanup Occupancy Policy
- `markedForDeath` 또는 `hp <= 0` entity는 Cleanup 전까지 snapshot query에 남아 있을 수 있다.
- placement query와 impact target selection은 동일한 질문이 아니므로 분리해서 해석한다.
- verification should prefer:
  - `EnumerateUnitsAt(...)`
  - `TryPickImpactTargetAt(...)`
  - `TryGetUnitTraversalBlocker(...)`

## Determinism
- deterministic contract는 자료구조 iteration order가 아니라 explicit ordering policy로 보장한다.
- regression validation은 hash, trace equality, final entities, event log를 우선 본다.
- trace section name이나 synthetic normalized input token은 canonical contract가 아니다.
