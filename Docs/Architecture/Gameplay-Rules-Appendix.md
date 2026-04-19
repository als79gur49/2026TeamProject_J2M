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
- current live runtime producer가 emit하는 state는 `Anchored`, `Airborne`, `Phased`다.
- `Airborne`는 jump owner가 만든 explicit non-anchored state일 때만 인정한다.
- `Phased`는 `WorldState` authoritative carrier를 가지며 internal pre-movement owner가 live runtime에서 emit할 수 있다.
- current concrete live source는 `PlayerControlStateLogic`의 player flip windup window와 `EnemyLogic`의 locked-target cross-through validator다.
- `Attached`는 아직 reserved future state이며 legality/query consumer도 열지 않는다.
- `Anchored`는 기본 spatial mode다. `Detached`라고 해서 자동으로 `Airborne`가 되지 않는다.

## Phased
- 이번 단계에서 고정하는 것:
  - `Phased` live seam은 traversal에서 `Unit`/`Solid` blocker bypass capability를 이해한다.
  - `Phased` live seam은 fresh target acquisition suppression을 이해한다.
  - current enemy current-lock path만 `existing lock retention`을 narrow hook로 사용한다.
  - `Phased`는 `Terrain`, `BoardEdge`, `Reservation` bypass를 뜻하지 않는다.
  - current live profile은 `ClaimsAuthoritativeOccupancy=true`와 active-face visibility, anchored-like terminal settle을 `StageDefault`로 사용한다. 이것은 current implementation default이지 future invariant가 아니다.
  - current live enter/sustain/exit owner는 `MovementPreMovement`, `EnemyPreMovement`, `DebugForced`로 metadata table에 고정한다.
  - first minimal consumer는 `EnemyPreMovement`에서 실행되는 `locked-target cross-through` validator이며 fresh target search, fallback, multi-edge pathfinding, same-tick damage coupling을 열지 않는다.
  - 이 consumer는 validator, not a movement framework다. current lock이나 geometry가 닫히면 reject/close하고 같은 tick에 ordinary movement, fallback reroute, combat attack으로 우회하지 않는다.
- 이번 단계에서 고정하지 않는 것:
  - future non-claim / overlap model
  - future visibility variants
  - future settlement overlap semantics
  - multi-source arbitration / generalized phase framework
  - attack-owned / delayed-effect / scripted-debug source
- interpretation rule:
  - current live owner는 internal `PreMovementState` write-path다.
  - carrier truth-source는 `PhasedRuntimeState` 하나뿐이며 caller-local bool 조합으로 추론하지 않는다.
  - earliest live observation point는 pre-movement batch 이후 `planSnapshot`이다.
  - sibling pre-movement logic는 same-pass enter/exit를 관측하지 못한다.
  - later clear/cancel은 이후 snapshot부터만 보이고 earlier snapshot을 retroactive하게 바꾸지 않는다.
  - current live source가 다른 owner와 충돌하면 explicit clear/replace ordering 없이는 공존하지 않는다.
  - `EnemyPreMovement` validator consumer의 direct reservation read는 terminal settle 직전 `cell` 한 번뿐이다. direct `edge/entity/topology-exclusive` reservation read는 out-of-scope다.

## Targetability
- targetability participation은 traversal/settlement blocker vocabulary와 별도 축이다.
- current seam rule:
  - base default는 spatial fact에서 시작한다.
  - current v1 default는 `SpatialStateSemantics`가 제공하고 effective seam은 `ModifierQuery`를 거친다.
  - blocker enum에 targetability-only kind를 추가하지 않는다.
  - current `existing lock 유지`는 current enemy lock path에만 한정한다.
  - future `impact-only suppression`, `detection-only suppression`, broader `existing lock 유지`는 `ModifierQuery` typed-evidence hook에서만 연다.
- Deferred lock taxonomy:
  - current `existing lock 유지`는 `EnemyActionStateTargeting.TryResolveLockedTarget(...)` current enemy path 전용 stage-scoped exception이다.
  - future taxonomy 후보 이름은 `detection lock`, `impact lock`, `scripted/debug lock`, `UI/presentation selection lock`, `future AI pursuit lock`까지만 기록한다. semantics는 이번 단계에서 열지 않는다.
  - 다음 단계 implementer는 current helper가 generic lock framework의 seed라고 가정하면 안 된다.
  - 다음 단계 implementer는 다른 lock 종류가 current enemy retention helper를 재사용해도 된다고 가정하면 안 된다.
  - 다음 단계 implementer는 `FreshSelectionSuppressedWithCurrentEnemyLockRetention` enum이 taxonomy 완성형이라고 가정하면 안 된다.
  - 새 lock class를 열려면 새 evidence type, 새 use-site decision, 새 tests가 필요하며 current helper rename/generalization으로 해결하지 않는다.

## Airborne Jump Landing
- `Airborne` actor는 traversal 중에는 일반 occupancy blocker로 취급되지 않는다.
- jump landing settlement에서는 requested terminal state가 `Anchored`로 돌아오며 landing cell legality를 다시 판정한다.
- anchored blocker가 landing cell을 차지하고 있으면 settlement가 blocked일 수 있다.

## Reservation
- `FrozenMovementReservationExport` contract는 유지한다.
- 이번 단계 reservation generalization 범위는 legality read seam용 `ReservationQuery` adapter까지만 허용한다.
- reservation detail 확장이 필요해져도 current public blocker vocabulary는 `Reservation` top-level kind를 유지한다.

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
