# ADR-001 Tick Boundary And IR Visibility

## Status
- Accepted

## Context
- `WorldState`와 `WorldSnapshot`은 이미 layered occupancy model을 구현하고 있는데, 문서와 테스트는 `TryGetUnitAt`, `IsBlockedForUnit`, `BlocksMovement`를 구조 vocabulary처럼 취급하고 있었다.
- host runtime 일부는 `TickResult.PresentationData` 대신 `CleanupPhaseResult`를 직접 읽고 있었다.
- attack path에는 `AttackIntent`, `AttackInputKind`, synthetic intent normalization, `PhaseTransientBuffer` 같은 implementation IR이 public/canonical language에 노출돼 있었다.

## Decision
- canonical query vocabulary는 layered query 중심으로 정리한다.
  - `EnumerateUnitsAt`
  - `TryGetSolidOccupantAt`
  - `TryPickImpactTargetAt`
  - `TryGetUnitTraversalBlocker`
- `TryGetUnitAt`, `IsBlockedForUnit`, `BlocksMovement`는 legacy compatibility API로 강등한다.
- `ImpactReservation`은 유지한다. 다만 external public contract가 아니라 inter-phase gameplay contract로 본다.
- `AttackIntent`, `AttackInputKind`, synthetic normalized input shape는 phase-private IR로 본다.
- `PhaseTransientBuffer`는 historical shim으로 남기고, narrower role name `ImpactReservationBuffer`를 기준으로 정리한다.
- host/view는 `TickResult.PresentationData`만 소비한다.

## Consequences
- black-box scenario tests는 impact/blocked/recovery, cleanup occupancy, determinism, presentation isolation 중심으로 이동한다.
- trace token schema와 synthetic normalized input shape를 직접 검증하는 테스트는 우선순위가 내려간다.
- old blueprint / implementation / governance docs는 non-canonical historical documents로 강등한다.

## Follow-up Decisions
- `ActionGroup`, `ActionGroupKind`, `ActionGroupComparer`
  - 이번 시리즈에서는 runtime core 내부 compatibility IR로 유지한다.
  - public/test/doc/trace surface에서 먼저 끊고, 이후 runtime core 밖 참조가 정리되면 `internal`로 축소한다.
- `ImpactReservation`
  - semantic contract는 유지한다.
  - `SourceActionGroupId`, `ReservationSequence`는 trace/tests decoupling 이후 제거 대상이다.
- `ImpactReservationBuffer`
  - 최종 inter-phase handoff는 `ImpactReservationBuffer`로 본다.
  - explicit `MovementPhaseResult -> Attack input` handoff는 현재 `MovementPhaseResult`가 `SortedIntents`, `ExpandedCandidates`, `PlanFinalizationBatch`, `PreMovementStatePhaseResult` 등 internal IR aggregate를 포함하므로 도입하지 않는다.
- friendship
  - host runtime이 `TickResult.PresentationData`만 소비하도록 유지하고, `Game.Feature.Gameplay.Host`에 대한 `InternalsVisibleTo`는 후속 cleanup 대상이다.
- internal naming
  - `PreMovementState` family rename은 boundary closeout 이후 별도 internal naming cleanup으로 미룬다.
