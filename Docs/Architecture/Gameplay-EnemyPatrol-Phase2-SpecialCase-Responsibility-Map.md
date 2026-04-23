# Enemy Patrol Phase 2: `EnemyLogic` Special-Case Responsibility Map

이 문서는 active architecture supporting truth-source이며, entrypoint는 [README.md](./README.md)다.

## 1. 단계 2 목표 요약

단계 2의 목표는 `RandomWalk`가 아직 `EnemyLogic`에 남겨 둔 patrol special-case를 구현 변경 없이 책임 기준으로 고정하는 것이다.

- `EnemyPatrolRuntimeState`는 계속 canonical patrol state 저장소로 유지한다.
- 단계 2는 docs-first 단계이며 patrol 시스템 전면 개편 단계가 아니다.
- 향후 공통화 후보 경계는 public interface가 아니라 내부 decision seam 문서화에 한정한다.

## 2. 현재 상태와 왜 단계 2가 필요한가

현재 `RandomWalk`는 `EnemyLogic` 내부에서만 `_patrolStrategyKind`를 직접 보고 planner를 호출하는 bounded seam이다. 실제 책임은 `EnemyLogic`, `EnemyRandomWalkPatrolPlanner`, `MovementCommitter`, `IEnemyAiStateResolver`로 분산되어 있다.

이 분산을 문서 없이 바로 일반화하면 다음 오인이 생긴다.

- planner가 `RawMovementIntent` 생성과 canonical patrol state write까지 소유한다고 오인할 수 있다.
- patrol decision과 `Patrol -> Chase -> Attack -> Recover` 전환을 한 레이어의 책임으로 합칠 위험이 있다.
- `Forward`와 `WallFollow`를 같은 난이도의 공통화 후보로 잘못 분류할 수 있다.

단계 2는 이 오인을 막고, 단계 3에서 `Forward`만 보수적으로 공통화 후보로 검토할 수 있게 만드는 책임 분리 준비 단계다.

## 3. 이번 단계 범위

### 포함

- `EnemyLogic` patrol 관련 책임을 메서드 단위로 분해한다.
- `RandomWalk` current seam을 `init -> plan -> facing -> intent -> commit -> chase transition` 순서로 정리한다.
- future patrol decision adapter / common decision layer 후보 경계를 내부 개념으로 정의한다.
- `Forward`를 다음 공통화 후보로 올릴 수 있는 readiness 기준을 만든다.
- `WallFollow`를 별도 bounded task로 남겨야 하는 이유를 고정한다.
- 단계 3 gate, no-touch list, rollback / defer criteria를 문서화한다.

### 제외

- `IPatrolStrategy` / `IPatrolFacingStrategy` 공통 시그니처 변경
- `Forward`, `WallFollow`, `Stationary` 삭제 또는 legacy 단정
- `WallFollow` 재설계
- `JumpChaser`, `Charge`, `TutorialPassiveContact`, `WallFollower` 기본 patrol 정책 변경
- stage-content canonical path, builder/result gameplay-only boundary, topology rule, deterministic chooser 재설계, broad backlog recovery

## 4. `EnemyLogic` current special-case 정리

### 현재 seam 요약

`RandomWalk` current seam은 아래 흐름으로 고정한다.

1. AI 전환은 `_stateResolver.Resolve(...)`가 결정한다.
   - patrol / chase / attack / recover 전환 owner는 `EnemyLogic`이 아니라 state resolver다.
2. patrol facing은 `EnemyLogic.ResolvePatrolFacing(...)`가 stage별로 결정한다.
   - `RandomWalk`는 planner 결과를 재사용하지만 facing write owner는 계속 `EnemyLogic`이다.
3. patrol state 초기화는 `EnemyLogic.CommitPreMovementState(...)`에서 `TryInitializeRandomWalkPatrolState(...)`를 통해 수행된다.
4. patrol movement intent 생성은 `EnemyLogic.ResolveBaselineGroundLocomotion(...)`가 수행한다.
   - `RandomWalk`만 `_patrolStrategyKind == PatrolStrategyKind.RandomWalk`를 직접 해석해 planner를 호출한다.
5. accepted move 이후 canonical patrol state write는 `MovementCommitter.ResolveEnemyPatrolResolutions(...)`가 수행한다.
   - `EnemyPatrolQueries.CommitMove(...)` 적용 owner는 movement commit이다.

### `EnemyLogic` patrol responsibility 표

| 책임 surface | 현재 위치 | 현재 owner | 단계 2 판정 |
| --- | --- | --- | --- |
| `_patrolStrategyKind` 저장 | `EnemyLogic` ctor | `EnemyLogic` | 유지, dispatch entry로 문서화 |
| `_patrolStrategyKind` 직접 해석 | `ResolveBaselineGroundLocomotion`, `ResolvePatrolFacing`, `TryInitializeRandomWalkPatrolState` | `EnemyLogic` | 현재 special-case 범위로 명시 |
| planner 호출 wrapper | `BuildRandomWalkPlan` | `EnemyLogic` | wrapper는 Logic 유지 |
| candidate/leash/backtrack/weight/deterministic 선택 | `EnemyRandomWalkPatrolPlanner.BuildPlan` | planner | future common decision 후보 |
| patrol facing 결정 시 planner 재사용 | `ResolvePatrolFacing` | `EnemyLogic` + planner | facing write owner는 Logic 유지 |
| patrol state 초기화 | `CommitPreMovementState -> TryInitializeRandomWalkPatrolState` | `EnemyLogic` | phase 2 / 3 초기 패스에서도 Logic 유지 |
| movement intent 생성 | `ResolveBaselineGroundLocomotion` | `EnemyLogic` | shared locomotion surface로 유지 |
| accepted move 후 patrol state commit | `MovementCommitter.ResolveEnemyPatrolResolutions` | movement commit | planner / adapter 범위 밖으로 고정 |
| chase / attack / recover 전환 | `_stateResolver.Resolve` | state resolver | patrol common layer 범위 밖으로 고정 |

### `RandomWalk`만 special-case인 이유

`RandomWalk`만 special-case인 이유는 현재 `RandomWalkPatrolStrategy`가 의도적으로 throw하기 때문이다. 현재 bounded rollout 계약은 "`RandomWalk` patrol dispatch는 아직 `EnemyLogic`이 소유한다"다.

또한 현재 `IPatrolStrategy` 시그니처만으로는 아래 입력과 출력을 자연스럽게 표현하지 못한다.

- `tickIndex`
- `EnemyPatrolRuntimeState`
- `ShouldInitializeState`
- deterministic weighted choice 결과
- candidate mask

즉 `RandomWalk`는 "move intent 하나를 바로 내는 전략"이 아니라 "planning 결과를 먼저 만들고 Logic이 나머지 owner surface를 이어 붙이는 seam"으로 취급해야 한다.

## 5. planner vs logic boundary

### boundary 원칙

단계 2의 future common decision layer는 문서상의 internal target seam일 뿐이며, 실제 interface 추가를 의미하지 않는다. 문서상 이름은 `PatrolDecisionProposal`로 고정한다.

`PatrolDecisionProposal`은 아래 종류의 decision-only 정보를 담는 conceptual seam이다.

- `HasDirection`
- `PlannedDirection`
- `PlannedFacing`
- `CandidateMask`
- `ShouldInitializeState`

이 conceptual seam은 `RawMovementIntent` 생성 직전에서 끝나며, write context를 받지 않는다.

### planner vs logic boundary 표

| 판단 / 쓰기 surface | 현재 owner | 향후 common decision 후보 | 단계 2 고정 owner |
| --- | --- | --- | --- |
| traversable candidate 평가 | planner / shared helper | 예 | planner / common decision layer |
| leash / home / backtrack / weight 선택 | planner | 예 | planner / common decision layer |
| planned direction / planned facing 계산 | planner | 예 | planner / common decision layer |
| state init 필요 여부 계산 | planner | 예 | planner / common decision layer |
| `RawMovementIntent` 생성 | `EnemyLogic` | 아니오 | `EnemyLogic` |
| locomotion cooldown 적용 | `EnemyLogic` | 아니오 | `EnemyLogic` |
| `SetEnemyPatrolState` 초기 write | `EnemyLogic` | 아니오 | `EnemyLogic` |
| accepted move 후 `CommitMove` write | `MovementCommitter` | 아니오 | movement commit |
| patrol ↔ chase / attack / recover 전환 | state resolver | 아니오 | state resolver |

### boundary 결론

기본 결론은 하나다.

- planner / common decision layer는 "방향 / 회전 / init hint를 제안"하는 곳까지가 범위다.
- "move accepted 이후 canonical state write"는 절대 planner / adapter로 가져가지 않는다.

`EnemyPatrolRuntimeState`의 canonical write owner는 계속 두 군데로 분리해 유지한다.

- 초기화 전 write: `EnemyLogic`
- accepted move 후 write: `MovementCommitter`

## 6. `Forward` 공통화 readiness 평가

`Forward`는 다음 단계의 후보로만 다루며, 이번 단계에서 실제 전환을 가정하지 않는다.

### readiness checklist

| readiness 항목 | 통과 기준 | 현재 판단 기준 |
| --- | --- | --- |
| decision-frame 적합성 | `Forward`가 `snapshot + source + settings`만으로 방향을 결정 가능 | 현재 충족 |
| stateless 유지 | `EnemyPatrolRuntimeState` read / write 없이 의미 보존 가능 | 현재 충족 |
| blocked semantics 보존 | `Stop` / `TryStepBackward` 의미가 그대로 유지 | 현재 충족 |
| facing semantics 단순성 | 현재 facing만 사용하고 별도 rotate-only 단계가 없음 | 현재 충족 |
| transition 분리 유지 | `Patrol -> Chase`, `Recover -> Patrol / Chase` trace 이유가 변하지 않음 | 단계 3 전 검증 필요 |
| 체감 유지 | `WindupMelee`의 전진 추격 전감과 shipping된 `NonAttacking` `RandomWalk` pilot 체감이 모두 무변경 | 단계 3 전 검증 필요 |

### 단계 2 verdict

`Forward`는 **조건부로 다음 공통화 후보가 될 수 있다**. 다만 아래 semantics는 단계 3 전까지 바뀌면 안 된다.

- `ForwardPatrolStrategy`의 blocked movement response (`Stop`, `TryStepBackward`)
- 현재 facing 기반 전진 의미
- state resolver가 소유하는 `Patrol -> Chase -> Attack -> Recover` 전환 이유
- `WindupMelee`, `JumpChaser`, `Charge`가 유지하는 기존 기본 patrol 종류

### `Forward` evidence 기준

단계 3 전에는 최소 아래 evidence를 다시 확인해야 한다.

- `EnemyLogic_PatrolMode_ProducesForwardMovementIntent`
- `ForwardPatrolStrategy_BlockedMovementResponseSetting_ChangesMovementOutcome`
- `EnemyAi_MultiTick_FollowsPatrolChaseAttackRecoverSequence`
- `EnemyAi_WindupProfile_LosingLockedTarget_CancelsActionAndFallsBackToPatrol`
- `EnemyAiProfileAssets_PatrolPilotRollout_MatchesExpectedPatrolKinds`
- `EnemyAi_NonAttackingRandomWalkPilot_OpenRoom_VisitsMultipleCellsAndKeepsMoving`
- `EnemyAi_NonAttackingRandomWalkPilot_AfterLosingTarget_ReturnsTowardHomeThenResumesPatrol`

## 7. `WallFollow` out-of-scope note

`WallFollow`는 legacy가 아니라 active bounded strategy이며, 단계 2의 공통화 후보가 아니다.

| `WallFollow` 특수 규칙 | 왜 지금 공통화 후보가 아닌가 | 단계 2 처리 |
| --- | --- | --- |
| wall / box / board-edge anchor 유지 및 재획득 | 단순 direction chooser보다 상태 없는 공간 규칙이 복합적이다 | 별도 bounded task |
| dead-end rotate-only facing | movement 없음과 facing update가 분리된다 | 공통 decision frame 밖 |
| same-cell passive contact hold | `EnemyLogic`가 movement 자체를 억제한다 | `RandomWalk` / `Forward`와 다른 owner surface |
| same-tick turn+move / dead-end rotate-then-resume | wall-follow 전용 scenario 의미가 강하다 | 단계 2에서 재설계 금지 |

단계 2에서 `WallFollow`는 다음과 같이 취급한다.

- `IPatrolFacingStrategy`까지 사용하는 특수 전략으로 유지한다.
- `SameCellPassiveContact` hold와 rotate-only facing은 별도 bounded task의 입력으로 남긴다.
- 단계 3의 `Forward` 검토 범위에 끌어오지 않는다.

## 8. 단계 3 진입 gate

단계 3는 아래 gate가 모두 충족될 때만 연다.

- `EnemyLogic` responsibility 표와 planner vs logic boundary 표가 완결돼 있다.
- `RandomWalk` seam에서 `init`, `plan`, `facing`, `intent`, `commit`, `chase transition` owner가 서로 구분돼 있다.
- future common decision 경계가 `RawMovementIntent` 생성 이전에서 끝난다고 문서로 고정돼 있다.
- `Forward` readiness checklist가 green이거나, red 항목이 모두 명시적 defer로 관리된다.
- `WallFollow` out-of-scope note가 별도 bounded task로 닫혀 있다.
- 기존 evidence로 `RandomWalk` pilot determinism, `Forward` baseline semantics, `WallFollow` 특수성이 모두 뒷받침된다.

아래 조건이 하나라도 생기면 단계 2는 docs-only로 닫고 단계 3를 열지 않는다.

- `IPatrolStrategy` / `IPatrolFacingStrategy` 변경 필요
- `EnemyPatrolRuntimeState` 구조 변경 필요
- `WallFollow` rule 흡수 필요
- deterministic chooser 재설계 필요
- topology rule 변경 필요

## 9. 테스트 / 검증 evidence

### unit

- `EnemyRandomWalkPatrolPlanner_*`
- `EnemyLogic_NonAttackingRandomWalkPatrol_InitializesAndCommitsPatrolState`
- `EnemyLogic_PatrolMode_ProducesForwardMovementIntent`
- `ForwardPatrolStrategy_BlockedMovementResponseSetting_ChangesMovementOutcome`
- `WallFollowPatrolStrategy_*`

### replay / determinism

- `DeterminismHash_EnemyPatrolState_IsIncludedInCanonicalState`
- `Replay_RandomWalkPilotProfile_ProducesStablePerTickHashTraceAndPatrolDump`

### scenario

- `EnemyAi_NonAttackingRandomWalkPilot_OpenRoom_VisitsMultipleCellsAndKeepsMoving`
- `EnemyAi_NonAttackingRandomWalkPilot_AfterLosingTarget_ReturnsTowardHomeThenResumesPatrol`
- `EnemyAi_MultiTick_FollowsPatrolChaseAttackRecoverSequence`
- `EnemyAi_WallFollowerProfile_WithLocomotionCooldown_PreservesWallFollowRule`
- `WallFollowPatrolStrategy_DeadEnd_RotatesInPlaceBeforeResumingPatrol`
- `DefaultEntityLogicProvider_WallFollowerProfile_ForwardBlocked_TurnsAndMovesInSameTick`

### authoring

- `EnemyAiProfileAssets_PatrolPilotRollout_MatchesExpectedPatrolKinds`

기본 원칙은 "기존 테스트를 근거로 문서를 잠그고, 문서 주장을 위해 새 runtime behavior를 만들지 않는다"다.

## 10. no-touch list

단계 2에서 건드리지 않는 대상은 아래로 고정한다.

- `IPatrolStrategy` / `IPatrolFacingStrategy` public contract
- `Forward`, `WallFollow`, `Stationary` 존재 자체
- `JumpChaser`, `Charge`, `TutorialPassiveContact`, `WallFollower` 기본 patrol 정책
- `EnemyPatrolRuntimeState` 외 추가 canonical patrol state 저장소
- stage-content canonical path
- builder / result gameplay-only boundary
- topology rule
- deterministic chooser 재설계
- broad backlog recovery

## 11. rollback / defer criteria

아래 중 하나라도 발생하면 이 단계는 docs-only close로 유지하고 다음 단계 구현을 연기한다.

- owner surface를 문서로 하나로 고정할 수 없을 정도로 책임이 겹친다.
- `Forward` 공통화 readiness의 red 항목이 evidence 없이 남는다.
- `WallFollow`를 설명하기 위해 common decision layer 정의가 흔들린다.
- `EnemyPatrolRuntimeState` canonicality를 포기하거나 우회 저장소를 추가해야 한다.
- planner 범위를 `RawMovementIntent` 생성이나 post-commit write까지 확장해야 한다는 요구가 나온다.

## 12. 최종 성공 기준

- 한 문서만 읽어도 `EnemyLogic`가 지금 patrol에서 직접 하는 일과 하지 않는 일이 구분된다.
- `RandomWalk` special-case seam이 "임시 구현"이 아니라 "명시된 bounded seam"으로 고정된다.
- `Forward`는 다음 후보인지 아닌지가 evidence 기반으로 판정된다.
- `WallFollow`는 범위 밖이라는 이유가 재설계 없이 설명된다.
- 단계 3를 열어도 되는지, 아니면 단계 2를 docs-only로 닫아야 하는지가 gate로 결정된다.
