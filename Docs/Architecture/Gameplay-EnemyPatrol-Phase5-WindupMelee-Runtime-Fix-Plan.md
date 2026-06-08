# Enemy Patrol Phase 5: `WindupMelee RandomWalk pilot` Runtime Fix Plan

> Historical supporting note.
>
> 이 문서는 phase 5 close 전 bounded runtime fix touch set을 기록한 historical supporting note다.
> current active close gate가 아니며, current active truth는 [Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md)다.

phase 5는 아직 close가 아니다.

이 문서는 `WindupMelee RandomWalk pilot`의 남은 runtime red 3축만 닫기 위한 historical bounded runtime fix truth다. Retired `EnemyAi_WindupMelee.asset` profile은 current repository inventory가 아니며, `EnemyBrain_WindupMelee.asset` shared brain은 Stage-reachable WindupProjectile path가 사용하므로 유지한다. `Forward` fallback/oracle, `NonAttacking` pilot, `JumpChaser`, `Charge`, `WallFollow`, `TutorialPassiveContact` 기본 patrol 정책은 바꾸지 않는다.

## 1. Summary
- 목표: `AttackCommitted`, `active-windup entry`, `duplicate CommittedMove`를 same-revision targeted Unity evidence로 다시 닫는다.
- 원칙: helper/oracle hardening이 아니라 bounded runtime fix다.
- same-revision only evidence만 close retry gate로 사용한다.
- phase 5 close를 선가정하지 않는다.

## 2. runtime touch set
| surface | bounded change |
| --- | --- |
| `EnemyActionStateLogic` | `AttackCommitted` start gate 진단과 long move-lock bounded start carve-out |
| `TickPipeline` | execute gate 진단과 long move-lock bounded execute carve-out |
| `TickTraceFormatter` | actual write/semantic green 이후 trace duplication 제거 |

## 3. Gate Order
### 3.1 `AttackCommitted`
1. fixture/control self-check
2. start gate
3. execute gate
4. commit trace gate
5. recover gate

- `WindupContractMetrics`는 trace transition, `PresentationData.EnemyActionSignals`, authoritative action state를 함께 본다.
- start gate는 `TryResolveStartAction`, `CanStartAction`, move execution lock, started signal vs active action state를 분리한다.
- execute gate는 combat raw intent, `AttackRejected|Stage=ExecutionLock`, executionAttempted, accepted combat damage를 분리한다.

### 3.2 `duplicate CommittedMove`
1. actual write
2. trace duplication
3. semantic correlation
4. formatter 수정 허용 조건 확인

- actual write가 `1`이 아니면 formatter 수정은 금지한다.
- trace provenance는 `Movement.CommitEvents`와 `TickResult.EventLog`를 따로 본다.
- semantic correlation은 accepted `MoveCommitted` event와 actual patrol write 1:1 대응을 요구한다.

### 3.3 `LockedTargetLost`
1. active-windup entry
2. cancel owner
3. wording / trace
4. home-return

- conditional-fix 단계로 다룬다.
- entry green 이전에는 wording fix를 하지 않는다.
- cancel owner red일 때만 `EnemyActionStateLogic` active-action lost-target branch와 fallback ordering을 본다.
- home-return red일 때만 stale combat execution / extra patrol commit bounded guard를 본다.

## 4. artifact / evidence order
1. `baseline-control-targeted.xml/.log`
2. `unit-runtime-targeted.xml/.log`
3. `scenario-targeted.xml/.log`
4. `replay-targeted.xml/.log`
5. `unit-authoring-doc-targeted.xml/.log`
6. `evidence-summary.md`

- summary는 항상 마지막에만 작성한다.
- artifact가 mixed revision이면 bundle 전체를 invalid로 본다.
- 중간 진단은 `attackcommitted-gate-summary`, `lockedtargetlost-conditional-summary`, `patrol-write-triage-summary`로 남긴다.

## 5. no-touch / rollback
- baseline asset destructive overwrite 금지
- `Forward` fallback/oracle 제거 금지
- `NonAttacking` pilot 회귀 금지
- `IPatrolStrategy` / `IPatrolFacingStrategy` 공통 시그니처 변경 금지
- proposal contract / deterministic chooser / topology rule / stage-content canonical path / builder-result boundary 변경 금지
- `RandomWalk` 전체 구조 재설계 금지
- broad backlog recovery 혼합 금지
- `EnemyLogic` init ownership 변경 금지
- `MovementCommitter` committed ownership 변경 금지

rollback 기준:
- baseline control green이 깨지면 즉시 되돌린다.
- same-cell ordering, move occupancy guard, replay hash, authoring/doc governance 중 하나라도 회귀하면 되돌린다.
- formatter 수정 후 semantic/event meaning이 바뀌면 되돌린다.

## 6. close retry gate
- baseline control 3축 green
- `AttackCommitted`의 start gate / execute gate / commit trace gate / recover gate green
- `active-windup entry -> cancel owner -> wording -> home-return` 4게이트 green
- `duplicate CommittedMove`의 actual write=1, trace=1, semantic correlation=1, triage=`None`
- replay targeted 5건 green
- baseline untouched / pilot reference unchanged / other archetype drift 없음
- documentation governance green

phase 5는 위 close retry gate가 same-revision targeted evidence로 모두 green이 되기 전까지 계속 open/red 상태를 유지한다.
