# ADR-004 Terrain And Occupancy Implementation Gate

- Status: Accepted
- Date: 2026-04-22

## Context

terrain / occupancy semantics는 stage-content와 분리된 dedicated gameplay semantics lane이다. authoring, query, legality, validation vocabulary를 먼저 닫고, 그다음 bounded slice implementation만 허용한다.

## Phase Split

- `E0. discovery`
  - authoring/query/legality/validation vocabulary와 live code seam을 조사한다.
- `E1. decision closed`
  - ADR/canonical spec update로 vocabulary와 경계를 잠근다.
  - 이 단계는 slice implementation start가 아니다.
- `E2. implementation gate ready`
  - gate가 green일 때만 slice implementation을 연다.
- `E3. slice implementation`
  - gate 이후 authoring/query/legality/validation slice를 순차 구현한다.

## Vocabulary Gate

- authoring/query/legality/validation vocabulary 확정
  - occupancy truth
  - terrain truth
  - blocker vocabulary
  - `Traverse`
  - `Settle`
  - `Modifier`
  - `Reservation`
- 위 용어는 서로 충돌 없이 single interpretation으로 잠겨 있어야 한다.

## Boundary Gate

- `Traverse / Settle / Modifier / Reservation` 경계 문서화 완료
- canonical spec와 appendix가 같은 vocabulary를 사용
- `Finalize no recheck`와 `WorldState authoritative`가 다시 명시됨

## Minimal Test Harness

- architecture tests
- `Tools/check_gameplay_semantic_query_migration.py`
- legality context governance tests
- target slice용 scenario/unit harness

## Quarantine Inventory Gate

- compatibility helper와 allowlist는 현재 상태로 동결한다.
- 새 slice가 allowlist나 compatibility helper를 무단 확대하면 implementation gate를 통과하지 못한다.

## Implementation Open Rule

- `E1` decision closed
- `E2` gate green
- 구현 slice는 한 번에 하나의 seam만 다룬다.
- 같은 change에서 doc/test/validator를 함께 갱신한다.

## Non-Goals

- decision closed를 근거로 즉시 runtime-wide semantics refactor 착수
- `Traverse/Settle/Modifier/Reservation` 경계를 한 PR에서 동시에 재정의
- box/wall/terrain/unit rule 혼합
- compat helper 부활을 통한 shortcut fix

## Related Decisions

- TileFeature overlay is not a terrain/occupancy extension. It follows [ADR-006 TileFeature Overlay Layer Gate](./ADR-006-TileFeature-Overlay-Layer-Gate.md).
