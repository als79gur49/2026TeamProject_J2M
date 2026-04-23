# Enemy Patrol Phase 3: `Forward` Commonization (Bounded)

이 문서는 active architecture supporting truth-source이며, entrypoint는 [README.md](./README.md)다.

## 1. 목표

phase 3의 목표는 `Forward`를 `RandomWalk`와 같은 bounded patrol decision frame 안의 simple strategy로 올리되, patrol 전체 통합이 아니라 `EnemyLogic` special-case를 줄이는 수준에서 구현을 닫는 것이다.

- `EnemyPatrolRuntimeState`는 계속 canonical patrol state 저장소로 유지한다.
- `Forward`는 stateless로 유지한다.
- phase 3는 `WallFollow` commonization 단계가 아니다.

## 2. Scope

### 포함

- `Forward`를 `EnemyPatrolDecisionProposal` support matrix에 추가한다.
- `EnemyLogic`를 single proposal seam consumer로 정리한다.
- `Forward` / `RandomWalk` proposal path의 owner surface delta를 문서와 테스트로 고정한다.

### 제외

- `IPatrolStrategy` / `IPatrolFacingStrategy` 시그니처 변경
- `WallFollow` 일반화
- `JumpChaser`, `Charge`, `TutorialPassiveContact`, `WallFollower` 기본 patrol 정책 변경
- topology rule, stage-content canonical path, builder/result gameplay-only 경계, deterministic chooser 재설계, broad backlog recovery

## 3. Phase 2 대비 Owner Surface Delta

| surface | phase 2 current owner | phase 3 target owner |
| --- | --- | --- |
| `Forward` direction / blocked fallback | `ForwardPatrolStrategy` direct path | `EnemyPatrolDecisionPlanner` proposal build |
| `RandomWalk` plan -> facing/init/intent hookup | `EnemyLogic` scattered special-case | `EnemyPatrolDecisionPlanner` + `EnemyLogic` proposal consumer |
| patrol kind dispatch | `EnemyLogic`가 method별 direct branch | `EnemyPatrolDecisionPlanner` support-matrix dispatch |
| pre-movement init write | `EnemyLogic` | `EnemyLogic` 유지 |
| committed patrol write | `MovementCommitter` | `MovementCommitter` 유지 |

## 4. Special-Case Reduction Checklist

phase 3 green은 아래 checklist가 모두 true일 때만 인정한다.

- `EnemyLogic`의 patrol kind 인지는 `TryBuildPatrolDecisionProposal(...)` 한 seam으로 축소된다.
- `ResolveBaselineGroundLocomotion`, `ResolvePatrolFacing`, `TryInitialize...PatrolState`는 proposal helper만 호출한다.
- `EnemyLogic.cs` 안에 `PatrolStrategyKind.Forward`, `PatrolStrategyKind.RandomWalk` direct branch가 남지 않는다.
- unsupported kinds는 existing strategy fallback path로 유지된다.

## 5. No-Touch List

- `IPatrolStrategy` / `IPatrolFacingStrategy` public contract
- `WallFollow` out-of-scope status
- `Forward` asset/profile reference
- `JumpChaser`, `Charge`, `TutorialPassiveContact`, `WallFollower` 기본 patrol 정책
- `EnemyPatrolRuntimeState` 외 새 canonical patrol state 저장소
- stage-content canonical path
- builder/result gameplay-only 경계
- topology rule
- deterministic chooser 재설계
- broad backlog recovery

## 6. Rollback Checklist

아래 중 하나라도 발생하면 phase 3 구현을 rollback하고 docs-only defer로 되돌린다.

- `Forward` proposal path가 patrol state read/write를 요구한다.
- `WallFollow` 규칙을 흡수해야만 proposal seam이 닫힌다.
- `EnemyLogic`가 proposal seam 밖에서 `Forward` / `RandomWalk` direct branch를 다시 가져와야 한다.
- `Forward` baseline unchanged, `RandomWalk` replay unchanged, patrol -> chase timing unchanged를 quantitative tests로 입증하지 못한다.
- `EnemyPatrolRuntimeState` 외 새 canonical patrol state footprint가 생긴다.

## 7. Success / Failure

### 성공 기준

- `EnemyLogic`의 patrol special-case가 kind별 다중 분기에서 single proposal seam + unsupported fallback으로 줄어든다.
- `Forward`는 proposal primary path로 들어오지만 stateless를 유지하고 새 patrol state write를 만들지 않는다.
- docs, README, source governance tests가 모두 green이다.

### 실패 기준

- direct kind branching 감소 효과가 없거나 문서로 증명되지 않는다.
- `Forward` unchanged semantics를 scenario / replay / unit evidence로 잠글 수 없다.
- future rollout gate 없이 phase 3 이후 정책이 열려 버린다.
