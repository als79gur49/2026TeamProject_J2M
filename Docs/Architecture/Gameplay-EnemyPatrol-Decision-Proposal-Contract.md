# Enemy Patrol Decision Proposal Contract

이 문서는 active architecture supporting truth-source이며, entrypoint는 [README.md](./README.md)다.

## 1. 목적

`PatrolDecisionProposal`은 `Enemy Patrol` bounded rollout의 stable internal contract다. 이 contract는 planner/common decision layer가 patrol decision-only 정보를 어디까지 제안할 수 있는지 고정한다.

- canonical patrol state 저장소는 계속 `EnemyPatrolRuntimeState` 하나다.
- proposal layer는 `RawMovementIntent`를 만들지 않는다.
- proposal layer는 cooldown, committed write, chase/attack/recover 전환을 소유하지 않는다.
- phase 3에서 supported simple kinds는 `Forward`, `RandomWalk` 두 개뿐이다.

## 2. Contract Shape

concrete runtime shape는 `EnemyPatrolDecisionProposal`이며, conceptual seam 이름은 계속 `PatrolDecisionProposal`로 유지한다.

| field | 의미 | owner note |
| --- | --- | --- |
| `HasDirection` | movement proposal이 있는가 | planner/common decision |
| `PlannedDirection` | proposal된 이동 방향 | planner/common decision |
| `PlannedFacing` | pre-movement facing hint | planner/common decision |
| `CandidateMask` | final proposal-eligible candidate bit mask | planner/common decision |
| `ShouldInitializeState` | pre-movement state init hint | planner/common decision |

`CandidateMask` bit meaning은 아래로 고정한다.

- `Up = 1 << 0`
- `Right = 1 << 1`
- `Down = 1 << 2`
- `Left = 1 << 3`

topology-changing step은 `CandidateMask`와 `PlannedDirection`에 절대 들어오지 않는다.

## 3. Supported Kinds

### `Forward`

- `Forward`는 stateless다.
- `EnemyPatrolRuntimeState`를 읽거나 쓰지 않는다.
- `PlannedFacing`은 항상 `source.facing`이다.
- `ShouldInitializeState`는 항상 `false`다.
- open forward면 forward bit만 candidate로 남긴다.
- blocked + `Stop`이면 no-direction / `CandidateMask = 0`이다.
- blocked + `TryStepBackward`이면 opposite direction만 candidate로 남긴다.

### `RandomWalk`

- `RandomWalk`는 existing `EnemyRandomWalkPatrolPlanner.BuildPlan(...)`를 adapter한 proposal path다.
- leash / weight / prevent-immediate-backtrack / deterministic chooser semantics는 adapter 밖에서 재설계하지 않는다.
- `ShouldInitializeState`는 existing random-walk plan semantics를 그대로 따른다.

## 4. Unsupported Kinds

아래 kinds는 이 contract의 current support matrix에 포함되지 않는다.

- `WallFollow`
- `Stationary`

이 둘은 proposal layer가 아니라 existing strategy path에 남는다.

- `WallFollow`는 out-of-scope다.
- `WallFollow`는 rotate-only facing, same-cell passive-contact hold, wall anchor semantics 때문에 simple proposal contract에 흡수하지 않는다.
- `Stationary`는 no-move semantics를 가진 existing strategy path로 유지한다.

## 5. Owner Boundary

| surface | owner |
| --- | --- |
| proposal build (`Forward`, `RandomWalk`) | planner/common decision layer |
| `RawMovementIntent` 생성 | `EnemyLogic` |
| locomotion cooldown 적용 | `EnemyLogic` |
| pre-movement `SetEnemyPatrolState` init write | `EnemyLogic` |
| accepted move 후 `CommitMove` write | `MovementCommitter` |
| patrol ↔ chase / attack / recover 전환 | state resolver |

boundary rule은 하나로 고정한다.

- planner/common decision layer는 proposal까지만 담당한다.
- `EnemyPatrolRuntimeState` 외 새 canonical patrol state 저장소를 만들지 않는다.
- proposal layer는 write context를 받지 않는다.

## 6. Phase 3 Policy

- `Forward`는 proposal primary path로 들어오지만 legacy/obsolete로 단정하지 않는다.
- `ForwardPatrolStrategy`는 parity oracle / rollback seam / authored semantics reference로 유지한다.
- `WallFollow`를 이 contract에 포함하지 않는다.
- `JumpChaser`, `Charge`, `TutorialPassiveContact`, `WallFollower` 기본 patrol 정책은 이 contract만으로 자동 rollout되지 않는다.
