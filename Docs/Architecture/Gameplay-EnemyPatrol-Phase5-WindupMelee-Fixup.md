# Enemy Patrol Phase 5: `WindupMelee` Runtime Parity Fixup

> Historical supporting note.
>
> 이 문서는 phase 5 close 전 `open/red -> close retry ready` 구간에서 사용한 runtime parity recovery path를 보존한다.
> current active close gate가 아니며, current active truth는 [Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md)다.

- baseline self-check hardening과 targeted close retry evidence 정리는 `Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md`에서 별도로 관리한다.
- close retry evidence는 baseline-control-first order와 `Current Green (non-close evidence)` vs `Current Red (close blockers)` split를 따라야 하며, summary truth는 `TestResults/phase5-red-closure/evidence-summary.md`에 남긴다.

## 1. 후속 수정 목표 요약
- 이번 문서의 목적은 phase 5를 다시 설계하는 것이 아니라 `WindupMelee RandomWalk pilot` 하나의 runtime parity red 묶음을 bounded fix로 닫는 것이다.
- retired `EnemyAi_WindupMelee.asset` profile은 current repository inventory가 아니다. Historical `EnemyBrain_WindupMelee.asset` shared brain 경로는 current taxonomy에서 Stage-reachable `EnemyBrain_WindupProjectile.asset` path로 정리됐다.
- `Forward` fallback/oracle, `NonAttacking` pilot, `JumpChaser`, `Charge`, `WallFollow`, `TutorialPassiveContact` 기본 patrol 정책은 이번 수정 범위가 아니다.

## 2. 현재 상태와 close 불가 이유
- 현재 상태는 `authoring/governance green`, `runtime parity red`다.
- phase 5는 아직 close가 아니다.
- 현재 red 묶음은 아래 네 가지다.
  - patrol state init / commit footprint
  - attack entry / `AttackCommitted` / recover parity
  - lose-target return-home semantics
  - replay / trace / dump footprint
- canonical patrol state 저장소는 계속 `EnemyPatrolRuntimeState` 하나다.
- current owner surface는 아래로 유지한다.
  - pre-movement init write: `EnemyLogic`
  - committed patrol move write: `MovementCommitter`
- fixup의 핵심은 first `Patrol -> Chase/Attack` 전에 patrol origin이 빠지는 absent path를 닫고, same revision에서 trace / replay / scenario evidence를 다시 잠그는 것이다.

## 3. bounded fix 범위
- 포함:
  - `WindupMelee RandomWalk pilot` runtime parity closure
  - patrol origin/init ownership 보정
  - attack entry / windup / recover parity 재검증
  - lose-target return-home 복구
  - replay / trace / dump footprint 확인
- 제외:
  - `RandomWalk` 전체 구조 재설계
  - `IPatrolStrategy` / `IPatrolFacingStrategy` 시그니처 변경
  - proposal contract / deterministic chooser / topology rule 변경
  - 다른 archetype rollout 확장

## 4. 실패 4개 묶음 triage
### 4.1 triage 표
| 실패 묶음 | 1차 의심 | 먼저 보는 코드 surface | 결정적 분리 신호 | 수정 lane |
| --- | --- | --- | --- | --- |
| patrol state init/commit footprint | first `Patrol -> Chase/Attack` 전에 home/init capture 누락 | `EnemyLogic.CommitAiTransitions`, `TryInitializePatrolStateFromProposal`, `MovementCommitter.ResolveEnemyPatrolResolutions` | first sensed tick 직후 snapshot에 patrol state가 있는지 | runtime state ownership |
| attack entry / `AttackCommitted` parity | 첫 divergence가 patrol인지 combat인지 불명확 | `ResolveBaselineGroundLocomotion`, default resolver, `EnemyActionStateLogic` | first divergence tick이 `TargetSensed` 전인지 후인지 | patrol lane 또는 combat lane |
| planner preset / scorecard | `ImmediateBacktrack == 0` 기대 과잉 vs 실제 planner bug | `EnemyRandomWalkPatrolPlanner.BuildPlan`, scorecard sampler | backtrack tick에 non-backtrack eligible 후보가 있었는지 | metric refinement 또는 planner fix |
| replay / trace / dump footprint | canonical state 부재 vs export omission | `TickTraceFormatter`, `TickReplayHarness`, `DeterminismHashBuilder` | final snapshot patrol entry와 dump가 같은 tick에 일치하는지 | state write 또는 export |

### 4.2 원인 후보 매핑 표
| 후보 | 설명 | 설명 가능한 실패 | 반증 조건 | 채택 시 조치 |
| --- | --- | --- | --- | --- |
| C1 | first `Patrol` 이탈 전에 patrol origin init이 없다 | patrol state footprint, replay dump `<empty>`, lose-target return-home | first sensed tick 이후 snapshot에 state가 있다 | `BeforeMovement` leaving-patrol path에서 one-time origin capture |
| C2 | pilot patrol divergence가 first-sense 전에 생긴다 | direct-lane / open-room attack parity | first divergence가 `To=Attack` 이후다 | patrol proposal / preset lane 재검토 |
| C3 | combat action start 또는 cancel path가 어긋난다 | `AttackCommitted`, `LockedTargetLost` mismatch | `AttackEntry`까지 exact다 | `EnemyActionStateLogic` / resolver lane triage |
| C4 | `leash=1`에서 forced backtrack을 scorecard가 bug로 오인한다 | scorecard failure | backtrack tick마다 대안 후보가 있다 | `avoidable immediate backtrack` 기준으로 분리 |
| C5 | export만 비고 snapshot은 채워져 있다 | replay / dump footprint | snapshot도 비어 있다 | formatter / harness-only fix |

## 5. runtime fix lane
### 5.1 patrol state init / commit
- `BeforeMovement`에서 `source.aiMode == Patrol`, `RandomWalk`, `proposal.ShouldInitializeState`, `no initialized patrol state`일 때만 one-time patrol origin을 먼저 capture한다.
- 이 보정은 `Patrol -> Chase/Attack` leaving path에만 적용한다.
- normal `Patrol -> Patrol` first-tick init은 기존 `TryInitializePatrolStateFromProposal(...)`를 그대로 유지한다.
- committed move write owner는 계속 `MovementCommitter.ResolveEnemyPatrolResolutions(...)`다.
- `Forward`는 끝까지 stateless다.

### 5.2 attack entry / windup / recover parity
- baseline contract는 아래를 유지한다.
  - `TargetSensed`
  - `TargetInRange`
  - `Attack entry`
  - `AttackCommitted`
  - `Recover entry`
  - `Recover complete`
- direct-lane은 exact, open-room은 later-only `+1` 이내다.
- `AttackEntry -> AttackCommitted`, `AttackCommitted -> RecoverEntry`, `RecoverEntry -> RecoverComplete` 간격은 exact로 유지한다.
- `LockedTargetLost`는 patrol dump footprint와 별도 gate로 본다.

### 5.3 planner preset / scorecard
- shipping preset은 계속 `leash=1`, `6 / 1 / 1`, `preventImmediateBacktrack=true`다.
- runtime fix 동안 preset은 변경하지 않는다.
- scorecard close 조건은 `avoidable immediate backtrack count == 0`로 읽는다.
- forced backtrack은 leash / home-return 제약에서 허용될 수 있지만, avoidable backtrack은 허용하지 않는다.

### 5.4 replay / trace / dump footprint
- hash / trace / dump가 모두 비면 export보다 upstream state absence를 먼저 의심한다.
- final snapshot에 patrol state가 있는데 dump가 `<empty>`면 formatter / harness lane으로 분리한다.
- lose-target fixture에서는 `LockedTargetLost` trace와 original home 기준 return을 함께 본다.

## 6. runtime parity closure checklist
- `WindupMelee` pilot은 first eligible patrol-origin tick 이후 canonical patrol state를 항상 가진다.
- patrol state write는 `Initialized`와 accepted `CommittedMove`만 남는다.
- chase / attack / recover 동안 unexpected patrol-state write가 없다.
- direct-lane에서 `TargetSensed`, `TargetInRange`, `Attack`, `AttackCommitted`, `Recover` tick이 baseline exact다.
- open-room에서 earlier drift는 `0`, later drift는 `+1` 이내다.
- lose-target tick에 `LockedTargetLost` trace가 나오고, 이후 movement는 original home 기준으로 줄어든다.
- replay dump / trace / determinism hash가 같은 tick의 patrol state footprint를 반영한다.
- `Forward` baseline / `NonAttacking` pilot / 다른 archetype authoring은 unchanged다.

## 7. same-revision evidence bundle
- `Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md`
- triage 표와 원인 후보 매핑 표가 포함된 문서 diff
- `TestResults/phase5-red-closure/baseline-control-targeted.xml`
- `TestResults/phase5-red-closure/baseline-control-targeted.log`
- unit pass readout
- replay / determinism pass readout
- scenario pass readout
- authoring / documentation governance pass readout
- direct-lane paired trace excerpt
- lose-target cancel / return-home trace excerpt
- replay patrol dump vs final snapshot correspondence proof
- baseline untouched / pilot-only binding proof
- `TestResults/phase5-red-closure/evidence-summary.md`

## 8. fallback / rollback
| surface | fallback | rollback trigger |
| --- | --- | --- |
| live binding | showcase entity `54`를 baseline profile로 복귀 | runtime parity gate가 red로 남음 |
| patrol-origin fix | leaving-patrol origin capture 제거 | `Forward` / `NonAttacking` regressions 발생 |
| preset decision | shipping preset 유지 | preset 변경이 combat timing 또는 authoring contract를 흔듦 |
| docs | phase 5 status를 `open/red`로 유지 | same-revision evidence bundle 미충족 |

## 9. phase 5 close gate
| gate | pass 조건 | evidence |
| --- | --- | --- |
| patrol state footprint | absent path 없음, init/commit ownership 고정 | unit + replay |
| attack parity | direct-lane exact, open-room bounded | unit + scenario |
| lose-target semantics | `LockedTargetLost` + original home return | scenario |
| replay / trace / dump | hash / trace / dump / snapshot 일치 | replay |
| non-regression | `Forward`, `NonAttacking`, other archetypes unchanged | replay + authoring |
| governance | rollout doc와 fixup doc가 충돌 없이 고정 | documentation tests |

## 10. close 실패 기준과 no-touch
- patrol state absent path가 남거나 replay dump `<empty>` 원인이 분리되지 않으면 close 불가다.
- `AttackCommitted`, `Recover`, `LockedTargetLost` 중 하나라도 exact / bounded contract를 못 맞추면 close 불가다.
- baseline untouched / fallback/oracle 유지 / other archetype no-drift를 same revision에서 입증하지 못하면 close 불가다.
- final no-touch:
  - retired `EnemyAi_WindupMelee.asset` profile 재도입 금지, current `EnemyBrain_WindupProjectile.asset` destructive overwrite 금지
  - `Forward` fallback/oracle 제거 금지
  - `NonAttacking` pilot 회귀 금지
  - `JumpChaser`, `Charge`, `WallFollow`, `TutorialPassiveContact` 기본 patrol 정책 변경 금지
  - `IPatrolStrategy` / `IPatrolFacingStrategy` 공통 시그니처 변경 금지
  - proposal contract / topology rule / deterministic chooser 변경 금지
  - broad backlog recovery 혼합 금지
