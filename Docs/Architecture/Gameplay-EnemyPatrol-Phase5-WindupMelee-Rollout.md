# Enemy Patrol Phase 5: `WindupMelee` Bounded Rollout

- 현재 상태: `closed`
- official close decision과 same-revision targeted close gate는 `Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md`에서 관리한다.
- runtime parity recovery path와 close retry hardening 과정은 `Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md`, `Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md`, `Gameplay-EnemyPatrol-Phase5-WindupMelee-Runtime-Fix-Plan.md` historical supporting note로 보존한다.

## 0. 현재 상태
- phase 5의 current truth는 `close retry ready -> close decision` 절차를 거쳐 `closed` 상태로 잠겼다.
- `TestResults/phase5-red-closure/evidence-summary.md`의 verdict는 계속 `Close Retry Ready` evidence verdict로 유지하며, final close decision은 `Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md`에서 분리 관리한다.
- `closed`는 same-revision targeted evidence bundle과 documentation governance lock이 승인되었음을 뜻한다.
- `closed`는 broad/full suite closed, `Forward` cleanup, other archetype rollout 승인, phase 6 자동 착수를 뜻하지 않는다.

## 1. Phase 5 목표 요약
- 이 historical phase의 목표는 `WindupMelee`를 `RandomWalk`의 두 번째 bounded pilot archetype으로 확장하되, 기존 전투 semantics와 authored baseline 체감을 검증 가능한 범위에서만 바꾸는 것이었다.
- historical live rollout은 showcase 단일 슬롯 1건만 opt-in 했다.
- `EnemyAi_WindupMelee.asset` repository profile은 Stage 미배치 cleanup에서 제거됐다. Current production windup lane은 Stage-reachable `EnemyAi_WindupProjectile.asset` / `WindupForwardCellProjectileCapabilityAsset` 경로다.
- `EnemyCore_WindupMelee.asset`와 `EnemyBrain_WindupMelee.asset`는 WindupProjectile shared authoring으로 남아 있으므로 유지한다.
- `Forward`는 계속 fallback oracle이다.
- `JumpChaser`, `Charge`, `TutorialPassiveContact`, `WallFollower` 기본 patrol 정책은 이 단계에서 바꾸지 않는다.

## 2. historical 상태와 왜 `WindupMelee`가 bounded pilot 후보였는지
- 현재 simple proposal support matrix는 `Forward`, `RandomWalk`만 공통 frame에 들어가며 `WallFollow`는 phase 4 verdict대로 independent bounded strategy로 유지한다.
- `WindupMelee` baseline은 historical authored fallback이었다. Current repository profile inventory에는 retired profile이 없고, current production windup behavior는 explicit WindupProjectile profile 기준이다.
  - baseline AI profile: retired repository profile, previously `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_WindupMelee/EnemyAi_WindupMelee.asset`
  - current windup AI profile: `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_WindupProjectile/EnemyAi_WindupProjectile.asset`
  - baseline core: `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Core/Enemy_WindupMelee/EnemyCore_WindupMelee.asset`
  - baseline brain: `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Brain/Enemy_WindupMelee/EnemyBrain_WindupMelee.asset`
  - patrol: `Forward`
  - detection: `NearestOpponent`
  - state resolver: `Default`
  - combat: melee + passive contact
  - historical windup / recover / locomotion timing은 authored asset 그대로 유지했다.
- 당시 `WindupMelee`는 `JumpChaser` / `Charge`보다 patrol 위 상위 resolver surface가 작았다.
  - `JumpChaser`는 jump windup / airborne / cooldown owner surface가 patrol semantics 위에 더 크게 얹힌다.
  - `Charge`는 dedicated resolver와 charge cadence owner surface가 patrol보다 더 크게 개입한다.
- 따라서 당시 `WindupMelee`는 `NonAttacking` 다음 bounded pilot 후보로 가장 보수적이었다.

## 3. historical baseline / rollout target / fallback / rollback
### 3.1 retired `WindupMelee` historical baseline
- retired baseline profile은 `Forward` patrol을 유지했다.
- retired baseline asset destructive overwrite는 금지였다.
- baseline은 phase 5 parity oracle이자 rollback seam이었다.

### 3.2 rollout target / fallback / rollback
| surface | target | fallback | rollback |
| --- | --- | --- | --- |
| baseline AI profile | retired repository profile | n/a | Stage 미배치 cleanup 이후 재도입 금지 |
| baseline brain | `EnemyBrain_WindupMelee.asset` untouched | same asset | overwrite 금지 |
| pilot patrol asset | retired repository asset | `EnemyPatrol_Forward.asset` | stage ref 제거 |
| pilot brain | retired random-walk pilot brain asset | baseline brain | stage ref 제거 |
| pilot AI profile | retired repository profile | baseline AI profile | stage ref 제거 |
| live rollout | showcase entity `54` 단일 슬롯 | baseline AI profile | entity `54` ref를 baseline으로 복귀 |

## 4. drift matrix
### 4.1 `exact-contract`
- `exact-contract` fixture에서는 drift `0 tick`만 허용한다.
- 대상 fixture는 `DirectLaneForwardBaseline`, `PrimedSameCellCombatPassive`, `RecoverCountdownReturn`다.
- exact 대상 이벤트는 아래로 고정한다.
  - `TargetSensed`
  - `TargetInRange`
  - `Attack entry`
  - `AttackCommitted`
  - `Recover entry`
  - `Recover complete`
- `AttackCommittedTick - AttackEntryTick`, `RecoverEntryTick - AttackCommittedTick`, `RecoverCompleteTick - RecoverEntryTick`는 exact 유지다.

### 4.2 `bounded-exposure`
- `bounded-exposure` fixture에서는 `earlier drift`를 금지한다.
- `later drift`만 `+1 tick`까지 허용한다.
- drift가 누적되면 실패다.
- 대상 fixture는 `OpenRoomOffset`, `OrthogonalFacingOpenRoom`, `CornerAdjacency`, `LoseTargetAndReturnHome`, `ShowcaseShadowReplay`다.
- 비교는 baseline `Forward`와 pilot `RandomWalk`를 같은 entity id, 같은 input sequence, 같은 board bounds로 돌리는 paired run 기준으로 한다.

## 5. pilot patrol preset scorecard
- shipping preset은 아래로 고정한다.
  - `leash=1`
  - `forward=6`
  - `side=1`
  - `backward=1`
  - `preventImmediateBacktrack=true`
- 이 값은 phase 5 runtime에서 임의 재탐색하지 않는다.
- 비교 control preset은 아래 두 개다.
  - control: `2 / 4 / 2 / 1`
  - reject-control: `1 / 8 / 1 / 0`
- shipping preset acceptance는 아래 네 조건을 모두 만족해야 한다.
  - `20-tick no-target open-room sample`에서 `max home distance <= 1`
  - forward candidate가 있을 때 committed move의 `60%` 이상이 forward
  - `avoidable immediate backtrack count == 0`
  - first combat damage tick이 baseline보다 빨라지지 않고 늦어져도 `+1` 이내
- shipping preset이 이 scorecard를 통과하지 못하면 phase 5는 실패다.

## 6. same-cell passive-contact / candidate ordering matrix
- same-cell matrix는 `WallFollow`식 hold를 여기에 끌어오지 않는다.
- `Patrol/Chase + no primed action` same-cell에서는 passive contact만 허용한다.
- 위 경우 combat candidate가 새로 생기면 실패다.
- `Attack + primed melee action + passive contact enabled` same-cell에서는 raw attack intent ordering이 반드시 아래와 같아야 한다.
  - `Combat(localSequence 0) -> PassiveContact(localSequence 1)`
- damage resolution ordering도 반드시 `[Combat, PassiveContact]`다.
- accepted / rejected shape는 반드시 `[Accepted Combat, Rejected PassiveContact(ReceiverCooldown)]`다.
- player HP delta는 정확히 `1`이어야 한다.
- `PassiveContact-only accepted tick count`가 baseline보다 증가하면 실패다.

## 7. evaluation sampling matrix
- live showcase slot 평가는 shipping authoring confirmation artifact일 뿐이다.
- acceptance 주 근거는 `test-only sampling matrix`다.
- sampling matrix는 아래 여섯 fixture로 고정한다.
  - `DirectLaneAligned`
  - `OpenRoomOffset`
  - `OrthogonalFacingOpenRoom`
  - `CornerAdjacency`
  - `LoseTargetDuringWindupThenReturnHome`
  - `PrimedSameCellCombatPassive`
- geometry axis는 `직선 lane`, `개방 room`, `벽 인접 corner`를 포함한다.
- state axis는 `Patrol origin`, `Chase origin`, `Attack primed`, `Recover return`, `LockedTargetLost`를 포함한다.
- contact axis는 `adjacent melee entry`, `same-cell stacked`를 모두 포함한다.
- 표본 확대는 live slot 추가가 아니라 test-only fixture 추가로만 한다.

## 8. authoring 변경 계획
- 새 pilot patrol asset을 추가한다.
  - retired random-walk pilot patrol asset
- 새 pilot brain을 추가한다.
  - retired random-walk pilot brain asset
- 새 pilot AI profile을 추가한다.
  - retired random-walk pilot profile asset
- pilot profile은 baseline `coreAuthoring`와 capability assets를 재사용한다.
- live rollout은 `stage-4-2.asset`의 entity `54` 한 슬롯만 pilot profile로 opt-in 한다.
- `Forward fallback untouched`가 authoring contract다.

## 9. 테스트 / 검증 계획
### 9.1 unit
- `EnemyLogic_WindupRandomWalkPilot_PatrolStateWrites_OccurOnlyOnInitAndCommittedPatrolMove`
- `EnemyAi_WindupRandomWalkPilot_AttackWindupRecoverContract_MatchesForwardBaseline`
- `EnemyRandomWalkPatrolPlanner_WindupPilotPreset_MeetsMeleeScorecard`
- `EnemyAi_WindupRandomWalkPilot_SameCellCombatPassiveOrdering_IsExact`

### 9.2 replay / determinism
- `Replay_WindupRandomWalkPilot_ProducesStableHashTrace_AndBoundedPatrolDump`
- `Replay_WindupRandomWalkPilot_DoesNotRegressForwardOrNonAttackingReplays`
- `DeterminismHash_WindupRandomWalkPilot_PatrolFootprint_IsLimitedToEnemyPatrolRuntimeState`

### 9.3 scenario
- `EnemyAi_WindupRandomWalkPilot_DirectLane_MatchesExactTransitionTicks`
- `EnemyAi_WindupRandomWalkPilot_OpenRoomOffset_DoesNotAdvanceAggressionEarlierThanBaseline`
- `EnemyAi_WindupRandomWalkPilot_LoseTargetDuringWindup_ReturnsHomeThenResumesPatrol`
- `EnemyAi_WindupRandomWalkPilot_PrimedSameCell_PreservesCombatThenPassiveOrdering`

### 9.4 authoring
- `MechanicsShowcaseStage_BuildsRandomWalkPilotProfileOverrideForWindupMeleeEnemy`
- `EnemyAiProfileAssets_WindupBaseline_RemainsForward_AndPilotVariant_IsRandomWalk`
- `EnemyAiProfileAssets_PatrolPilotRollout_MatchesExpectedPatrolKinds`

### 9.5 documentation governance
- `EnemyPatrolArchitectureReadme_ListsPhase5WindupRollout_AsSupportingTruthSource`
- `EnemyPatrolPhase5Doc_DefinesDriftMatrix_PilotScorecard_SameCellOrdering_Sampling_AndNextGate`
- `EnemyPatrolPhase5Doc_RepeatsForwardFallback_NoTouch_Rollback_SuccessFailure`

## 10. 단계별 실행 계획
### Step 1. Acceptance Matrix Freeze
- drift matrix, same-cell matrix, sampling matrix, next gate를 문서와 test name 수준으로 먼저 고정한다.
- runtime 변경, asset ref 변경, parameter 재탐색 금지.

### Step 2. Pilot Preset Scorecard Lock
- shipping preset `1 / 6 / 1 / 1`을 test-only scorecard로 잠근다.
- control / reject-control은 rationale 비교 artifact로만 사용한다.

### Step 3. Variant Authoring Add
- baseline untouched 상태로 pilot patrol / brain / AI profile을 추가한다.
- showcase entity `54`만 pilot profile로 전환한다.

### Step 4. Sampling Parity Suite
- exact-contract / bounded-exposure / same-cell ordering suite를 green으로 잠근다.

### Step 5. Governance Close
- README, phase 5 문서, authoring tests, replay tests, scenario tests를 닫는다.
- 다음 archetype은 자동 오픈 금지다.

## 11. post-phase decision matrix
| 상태 | `WindupMelee` baseline | pilot | `Forward` | 다음 gate |
| --- | --- | --- | --- | --- |
| success | untouched | showcase entity `54` bounded active | fallback oracle 유지 | readiness artifact 없이는 다음 archetype 오픈 금지 |
| failure | untouched | ref 제거 | fallback oracle 유지 | phase 5 rollback |

### 11.1 next archetype gate
- `JumpChaser` 오픈 전 필수 artifact:
  - `Docs/Architecture/Gameplay-EnemyPatrol-Phase6-JumpChaser-Readiness.md`
- 위 문서는 아래 항목을 포함해야 한다.
  - `jump windup / airborne / cooldown owner surface`
  - `patrol-vs-jump suppression matrix`
  - `landing retry semantics`
  - `patrol-state footprint boundary`
- `Charge` 오픈 전 필수 artifact:
  - `Docs/Architecture/Gameplay-EnemyPatrol-Phase6-Charge-Readiness.md`
- 위 문서는 아래 항목을 포함해야 한다.
  - `charge start gate`
  - `charge windup / active / recover cadence`
  - `active-phase passive-contact gating`
  - `charge-state write ownership`
  - `patrol-change 영향 한계`
- 공통 gate는 아래 네 개다.
  - phase 5 문서 / 테스트 완전 종료
  - `Forward fallback untouched`
  - baseline asset drift `0`
  - phase 5 waiver `0`

## 12. 성공 기준
- retired baseline `WindupMelee` asset은 당시 계속 `Forward`였다.
- retired pilot variant만 `RandomWalk`였다.
- exact-contract fixture는 drift `0`이다.
- bounded-exposure fixture는 earlier drift `0`, later drift `+1` 이내다.
- same-cell ordering / accepted-rejected shape / HP delta가 exact 유지다.
- replay / hash / trace는 deterministic하다.
- `EnemyPatrolRuntimeState` footprint만 bounded하게 늘어난다.

## 13. out-of-scope / no-touch
- `Forward` 제거 금지
- baseline `WindupMelee` destructive overwrite 금지
- `WallFollow` 재개입 금지
- `JumpChaser`, `Charge`, `TutorialPassiveContact`, `WallFollower` 기본 patrol 정책 변경 금지
- `IPatrolStrategy` / `IPatrolFacingStrategy` 시그니처 변경 금지
- proposal contract / deterministic chooser / topology rule 변경 금지
- stage-content canonical path, builder/result gameplay-only boundary 변경 금지
- broad backlog recovery와 혼합 금지

## 14. rollback / success / failure
- rollback 기준
  - shipping preset scorecard 실패
  - exact-contract drift 발생
  - bounded-exposure earlier drift 또는 stacked drift 발생
  - same-cell passive-contact ordering drift 발생
  - 다른 archetype patrol kind drift 발생
- failure 기준
  - `Forward fallback untouched` 조건을 유지할 수 없음
  - baseline asset destructive overwrite가 필요해짐
  - proposal contract / deterministic chooser / topology rule 변경이 필요해짐
- success는 phase 5 bounded rollout close일 뿐이며 `Forward` cleanup이나 phase 6 자동 착수를 의미하지 않는다.

## 15. closed 의미와 explicit non-claims
- `closed`는 `WindupMelee RandomWalk pilot` bounded rollout에 대한 official close decision이 승인되었음을 뜻한다.
- `closed`는 close gate의 canonical 근거가 same-revision targeted evidence bundle이라는 뜻이며, broad/full suite closure claim은 아니다.
- `closed` 이후에도 `EnemyBrain_WindupMelee.asset` shared brain, `Forward` fallback/oracle, other archetype no-touch 원칙은 current truth로 유지한다. Retired `EnemyAi_WindupMelee.asset` profile은 Stage 미배치 cleanup 이후 current repository inventory가 아니다.
- phase 6 readiness review는 close 이후 별도 단계이며, `JumpChaser` 또는 `Charge` rollout이 자동으로 열리지 않는다.
