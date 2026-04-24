# Enemy Patrol Phase 5: `WindupMelee RandomWalk pilot` Red Closure Hardening Plan

phase 5는 아직 close가 아니다.

이 문서는 `Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md`와 `Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md`의 후속 bounded fix / hardening truth다. 목표는 `WindupMelee RandomWalk pilot`의 남은 runtime parity red를 close로 선가정하지 않고, close retry를 시도할 수 있는 hard gate와 evidence 체계를 same-revision 기준으로 다시 잠그는 것이다.

baseline control green이 아니면 이하 증거는 close blocker 판정에 사용하지 않는다.

## 1. 유지할 강점
- helper/oracle red와 runtime red를 분리하는 hard gate 방향은 유지한다.
- baseline self-check 2축, replay targeted 4건, authoring/doc governance green은 유지 중인 invariant다.
- baseline `EnemyAi_WindupMelee.asset` / `EnemyBrain_WindupMelee.asset`는 untouched fallback/oracle로 유지한다.
- `Forward` fallback/oracle, `NonAttacking` pilot, `JumpChaser`, `Charge`, `WallFollow`, `TutorialPassiveContact` 기본 patrol 정책은 no-touch다.
- patrol origin capture fix를 유지한 채 red를 `AttackCommitted`, `LockedTargetLost`, `duplicate CommittedMove` 3축으로 좁힌 현재 상태는 close가 아니라 close retry hard gate를 더 정확히 만들 수 있는 상태다.

## 2. 현재 상태/계획의 약점
- `AttackCommitted` lane이 pre-combat drift, execute, trace emission, recover countdown을 한 덩어리로 본다.
- `LockedTargetLost` lane은 wording mismatch를 벗어났지만 fixture precondition failure와 runtime cancel owner failure를 충분히 분리하지 못한다.
- patrol ownership red는 사실상 `duplicate CommittedMove` 하나로 좁혀졌는데 actual write / trace emission duplication / semantic correlation mismatch 분리가 약하다.
- close retry checklist는 좋아졌지만 baseline control green이 선행 중단 조건으로 문서화되지 않아 실행 우선순위가 약하다.
- current green인 replay/authoring/doc와 current red인 unit/scenario runtime이 evidence와 문서에서 같은 레벨로 보일 여지가 있다.

## 3. 보완 목표
- baseline control green을 close retry의 0번 선행 gate로 격상한다.
- `AttackCommitted` lane을 `combat-start gate`, `execute gate`, `commit trace gate`, `recover gate` 4개로 고정한다.
- `LockedTargetLost` lane을 `active-windup entry`, `cancel owner`, `trace wording`, `home-return` 4개 gate로 고정한다.
- patrol ownership lane은 `actual duplicate write`, `trace-only duplication`, `semantic correlation mismatch` 3분류로 triage한다.
- current green과 current red를 문서, XML/log, `evidence-summary.md`에서 물리적으로 분리한다.
- runtime 수정 표면은 `EnemyLogic` / `EnemyActionStateLogic` 내부 bounded fix로만 제한한다.

## 4. baseline control 선행 gate
close retry 실행 순서는 아래로 고정한다.

1. baseline control
2. measurement hardening
3. `AttackCommitted`
4. `LockedTargetLost`
5. duplicate patrol ownership
6. replay / authoring / documentation governance re-lock
7. same-revision evidence bundle

baseline control self-check는 아래 3축이다.

| gate | control fixture | green 조건 | red 해석 |
| --- | --- | --- | --- |
| `EnemyAi_WindupForwardBaseline_AttackCommittedControlProbe_IsComplete` | forward direct-lane control | 4-gate attack/recover probe complete | helper / fixture / oracle red |
| `EnemyAi_WindupForwardBaseline_LockedTargetLostControlProbe_IsComplete` | forward cancel control | active-windup entry, cancel owner, wording complete | helper / fixture / oracle red |
| `Replay_ForwardProfile_ProducesStableHashTrace_AndNoPatrolStateWrites` | forward replay control | same revision hash/trace/no-write green | upstream state/oracle red |

이 3축 중 하나라도 red면 pilot runtime parity triage를 진행하지 않는다.

## 5. `AttackCommitted` lane 세분화 보강안
### 5.1 combat-start gate
- 측정 대상: `TargetSensedTick`, `TargetInRangeTick`, `AttackEntryTick`, `ActionStartTick`
- owner surface: patrol/chase resolver와 `EnemyActionStateLogic.CommitBeforeAttackCollection(...)` 경계
- green 조건: direct-lane exact, open-room later-only `+1` 이내, 최초 position/facing divergence가 baseline `TargetSensedTick` 이전이 아님
- red 해석: 여기서 실패하면 combat lane이 아니라 pre-combat drift lane이다

### 5.2 execute gate
- 측정 대상: `AttackExecuteTick`, `AttackExecuteActionStateActiveTick`, `AttackExecuteExecutionAttemptedTick`, `FirstCombatDamageTick`
- green 조건: `ActionStart -> AttackExecute`, `AttackExecute -> FirstCombatDamage` 간격 exact, execute tick에 action state active / executionAttempted 보유
- red 해석: combat-start gate green 이후 여기만 red면 `EnemyActionStateLogic` 또는 attack resolution owner red다

### 5.3 commit trace gate
- 측정 대상: `AttackCommittedTraceTick`, `AttackCommittedRecoverTick`
- green 조건: 두 tick이 모두 존재하고 같은 tick이다
- 금지 조건: trace만 있고 recover transition이 없거나, recover transition만 있고 trace가 없는 orphan 상태
- red 해석: execute gate green 이후 여기만 red면 runtime semantics가 아니라 transition emission / measurement lane red다

### 5.4 recover gate
- 측정 대상: `RecoverEntryTick`, `RecoverCompleteTick`, `RecoverTickCount`, recover 동안 `EnemyPatrolStateUpdated` count
- green 조건: `RecoverTickCount == commonSettings.RecoverTicks`, recover 동안 patrol write 0, `RecoverEntry -> RecoverComplete` 간격 exact
- red 해석: commit trace gate green 이후 여기만 red면 generic recover countdown lane이다

## 6. `LockedTargetLost` / active-windup 진입 보강안
- `active-windup entry`: target 제거 직전 tick에 `StartedThisTick` 또는 stored `EnemyActionState.IsActive`가 확인돼야 한다. 실패 시 runtime red가 아니라 fixture invalid red다.
- `cancel owner`: target 제거 직후 tick에 `CanceledThisTick`, action state cleared, fallback AI mode 적용이 같은 tick에서 확인돼야 한다. owner는 `EnemyActionStateLogic.ApplyCancelFallback(...)`와 action clear 경계다.
- `trace wording`: cancel owner gate green인 동일 tick에서만 `LockedTargetLost` wording을 검사한다. wording red는 cancel owner red와 합치지 않는다.
- `home-return`: cancel 이후 distance-to-original-home가 단조 비증가하고, leash 내 복귀 후 resumed patrol 동안 `<= 1`을 유지해야 한다.

실패 분류는 아래로 고정한다.

| 실패 분류 | 의미 |
| --- | --- |
| entry 미성립 | fixture/oracle red |
| entry green + cancel owner red | runtime cancel owner red |
| cancel owner green + wording red | trace wording red |
| cancel/wording green + return-home red | patrol/home semantics red |

## 7. patrol ownership duplicate write triage 보강안
canonical patrol ownership oracle은 유지한다. 허용 label은 `Initialized`, `CommittedMove`뿐이고 chase/attack/recover는 no-write zone이다.

triage는 trace parser 하나로 하지 않고 아래 3원 증거로 고정한다.

| evidence | 역할 |
| --- | --- |
| `SetEnemyPatrolState` write spy | canonical actual write source |
| `EnemyPatrolStateUpdated` trace parser | emission duplication 진단 |
| `MoveCommitted` semantic event correlator | semantic 1:1 correlation 검증 |

분류 기준은 아래다.

| triage | 기준 |
| --- | --- |
| `actual duplicate write` | 같은 tick actual `CommittedMove` write가 2회 이상이거나 final state sequence delta가 기대보다 큼 |
| `trace-only duplication` | actual write는 1회인데 trace parser가 같은 tick 같은 `CommittedMove`를 2회 이상 봄 |
| `semantic correlation mismatch` | actual write와 trace는 1회인데 accepted `MoveCommitted` semantic event가 0회거나 2회 이상이거나 다른 movement resolution에 매칭됨 |

close gate는 raw count가 아니라 `label set + exact multiplicity + same-tick uniqueness + semantic correlation + no-write zone` 조합으로만 연다.

## 8. green-vs-red evidence 분리 보강안
문서와 summary는 아래 두 표를 강제한다.

### Current Green (non-close evidence)
| lane | 상태 | 의미 |
| --- | --- | --- |
| baseline control | green | close blocker 판정의 선행 gate |
| replay targeted 4건 | green | non-close evidence |
| authoring/doc governance | green | non-close evidence |

### Current Red (close blockers)
| lane | 상태 | 의미 |
| --- | --- | --- |
| `AttackCommitted` | red | runtime close blocker |
| `LockedTargetLost` | red | runtime close blocker |
| `duplicate CommittedMove` | red | runtime close blocker |

green evidence는 red를 상쇄하는 자료로 쓰지 않는다.

## 9. 수정된 테스트 / 검증 계획
### unit
- `EnemyAi_WindupForwardBaseline_AttackCommittedControlProbe_IsComplete`
- `EnemyAi_WindupForwardBaseline_LockedTargetLostControlProbe_IsComplete`
- `EnemyLogic_WindupRandomWalkPilot_PatrolStateWrites_OccurOnlyOnInitAndCommittedPatrolMove`
- `EnemyLogic_WindupRandomWalkPilot_DoesNotWritePatrolState_DuringChaseAttackRecover`
- `EnemyAi_WindupRandomWalkPilot_AttackWindupRecoverContract_MatchesForwardBaseline`

### scenario
- `EnemyAi_WindupRandomWalkPilot_DirectLane_MatchesExactTransitionTicks`
- `EnemyAi_WindupRandomWalkPilot_OpenRoomOffset_DoesNotAdvanceAggressionEarlierThanBaseline`
- `EnemyAi_WindupRandomWalkPilot_LoseTargetDuringWindup_ReturnsHomeThenResumesPatrol`
- `EnemyAi_WindupRandomWalkPilot_PrimedSameCell_PreservesCombatThenPassiveOrdering`

### replay / determinism
- `Replay_WindupRandomWalkPilot_ProducesStableHashTrace_AndBoundedPatrolDump`
- `Replay_WindupRandomWalkPilot_PatrolDump_MatchesFinalSnapshotState`
- `Replay_WindupRandomWalkPilot_DoesNotRegressForwardOrNonAttackingReplays`
- `DeterminismHash_WindupRandomWalkPilot_PatrolFootprint_IsLimitedToEnemyPatrolRuntimeState`

### authoring
- `EnemyAiProfileAssets_WindupBaseline_RemainsForward_AndPilotVariant_IsRandomWalk`
- `EnemyPatrolAssets_WindupRandomWalkPilotAsset_UsesLockedMeleePreset`
- `CombinedGameplayStage_BuildsRandomWalkPilotProfileOverrideForWindupMeleeEnemy`

### documentation governance
- `EnemyPatrolPhase5DocumentationTests`
- baseline-control-first order
- green-vs-red split
- duplicate triage 3분류
- close retry non-assumption

## 10. same-revision evidence bundle
`TestResults/phase5-red-closure/` 아래 evidence 파일을 유지한다.

- `baseline-control-targeted.xml`
- `baseline-control-targeted.log`
- `unit-runtime-targeted.xml`
- `unit-runtime-targeted.log`
- `scenario-targeted.xml`
- `scenario-targeted.log`
- `replay-targeted.xml`
- `replay-targeted.log`
- `unit-authoring-doc-targeted.xml`
- `unit-authoring-doc-targeted.log`
- `evidence-summary.md`

`evidence-summary.md`는 revision SHA, 실행 일시, `Current Green (non-close evidence)` 표, `Current Red (close blockers)` 표, close-retry verdict를 분리 기록한다.

## 11. 수정된 단계별 실행 계획
### 11.1 Baseline Control Freeze
- 작업: baseline control helper를 0번 gate로 고정하고 `baseline-control-targeted.xml` / `.log`를 추가한다
- 완료 기준: attack-commit control, locked-target-lost control, forward replay control이 same revision green
- 금지 사항: pilot runtime code 수정 시작 금지, baseline asset touch 금지
- rollback 기준: control probe가 flaky하거나 baseline semantics와 다른 owner를 요구하면 helper 변경을 되돌린다

### 11.2 `AttackCommitted` Lane Split Hardening
- 작업: `WindupContractMetrics`와 parity helper를 4-gate 구조로 분해한다
- 완료 기준: direct-lane / open-room에서 red gate가 단일하게 식별된다
- 금지 사항: patrol preset 변경 금지, recover semantics를 맞추기 위한 baseline drift 허용 금지
- rollback 기준: `Forward` control green이 깨지거나 first divergence가 baseline `TargetSensed` 이전으로 당겨지면 되돌린다

### 11.3 `LockedTargetLost` / Active-Windup Entry Hardening
- 작업: lose-target scenario를 `entry -> cancel owner -> wording -> home-return`으로 분해한다
- 완료 기준: fixture invalid와 runtime cancel owner red가 분리된다
- 금지 사항: wording green을 위해 cancel owner 실패를 숨기지 않는다
- rollback 기준: active-windup entry가 불안정해지거나 cancel 후 fallback mode가 baseline과 달라지면 폐기한다

### 11.4 Patrol Ownership Duplicate Write Triage Hardening
- 작업: patrol write spy와 trace/semantic correlator를 결합해 duplicate를 3분류로 triage한다
- 완료 기준: `duplicate CommittedMove`가 세 분류 중 하나로만 귀속된다
- 금지 사항: `CommittedMove` contract 완화 금지, trace만 지우고 state write를 남기는 봉합 금지
- rollback 기준: spy 도입이 `Forward` / `NonAttacking` / replay dump에 footprint를 추가하면 되돌린다

### 11.5 Evidence / Docs Re-lock And Close-Retry Pack
- 작업: green-vs-red evidence split를 문서와 artifact에 반영하고 same-revision close-retry bundle을 재조립한다
- 완료 기준: `README`, fixup, red-closure doc, documentation tests, evidence summary가 동일한 판정을 말하고 close를 선가정하지 않는다
- 금지 사항: replay/authoring green을 runtime red 해소처럼 서술 금지, broad backlog recovery 혼합 금지
- rollback 기준: 문서가 close를 암시하거나 green/red를 혼합 서술하면 format을 재설계한다

## 12. 리스크와 완화책
- control helper false green/false red 위험: trace 단일 source가 아니라 trace + state + presentation signal 조합으로 고정한다
- lose-target fixture가 active windup에 진입하지 못하는 flaky 위험: active-windup entry를 별도 fixture invalid로 분리한다
- `duplicate CommittedMove`를 runtime write 문제로 오판할 위험: write spy를 canonical source로 승격하고 trace parser는 secondary oracle로 둔다
- replay/authoring green이 close 낙관론처럼 읽힐 위험: `Current Green (non-close evidence)`와 `Current Red (close blockers)` 2표를 강제한다
- bounded fix가 다른 archetype에 전파될 위험: `Forward`, `NonAttacking`, `JumpChaser`, `Charge`, `WallFollow`, `TutorialPassiveContact` no-touch regression suite를 유지한다

## 13. phase 5 close retry 성공 / 실패 기준
성공 기준:
- baseline control 3축이 same revision green
- `AttackCommitted` 4-gate가 direct-lane exact, open-room bounded 규칙을 모두 만족
- `LockedTargetLost` 4-gate에서 fixture invalid가 없고 cancel owner, wording, home-return이 green
- patrol ownership canonical oracle에서 `duplicate CommittedMove`가 해소되고 분류 잔여 red가 없음
- replay targeted 4건, authoring, documentation governance가 green이며 runtime red와 별도 표로 기록됨
- same-revision `TestResults/phase5-red-closure/` bundle과 `evidence-summary.md`가 완성됨

실패 기준:
- baseline control 3축 중 하나라도 red
- `AttackCommitted` 4-gate 중 하나라도 미완료이거나 최초 divergence가 baseline `TargetSensed` 이전
- lose-target lane에서 active-windup entry가 성립하지 않거나 cancel owner와 wording이 같은 failure로 뭉개짐
- `duplicate CommittedMove`가 actual write / trace-only duplication / semantic correlation mismatch 중 어디인지 분리되지 않음
- replay/authoring/doc green이 same revision으로 재확인되지 않거나 runtime red와 분리 보고되지 않음
- evidence bundle이 incomplete이거나 close 가정 문구가 들어감

## 14. no-touch / 최종 금지 규칙
- baseline asset destructive overwrite 금지
- `Forward` fallback/oracle 제거 금지
- `NonAttacking` pilot 회귀 금지
- `JumpChaser`, `Charge`, `WallFollow`, `TutorialPassiveContact` 기본 patrol 정책 변경 금지
- `IPatrolStrategy` / `IPatrolFacingStrategy` 공통 시그니처 변경 금지
- proposal contract / deterministic chooser / topology rule / stage-content canonical path / builder-result boundary 변경 금지
- `RandomWalk` 전체 구조 재설계 금지
- broad backlog recovery 혼합 금지
- replay green이나 authoring/doc green으로 runtime parity red를 덮어쓰기 금지
- baseline control green 이전에 close blocker triage를 확정하는 것 금지
- phase 5 close 선가정 금지

phase 5는 same-revision Unity targeted evidence가 모두 green일 때만 close retry를 다시 시도할 수 있다. 그 전까지는 open/red 상태를 유지한다.
