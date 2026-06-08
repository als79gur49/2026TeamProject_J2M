# Enemy Patrol Phase 5: `WindupMelee` Close Retry Execution

- 현재 상태: `closed`
- `TestResults/phase5-red-closure/evidence-summary.md`의 verdict는 계속 `Close Retry Ready`로 유지한다.
- official close decision, active truth-source hierarchy, close wording migration은 이 문서에서 관리한다.

## Scope
- 이번 close는 `WindupMelee RandomWalk pilot` bounded rollout의 문서 / evidence / governance close만 다룬다.
- canonical close gate는 `same-revision targeted evidence bundle`이다.
- runtime gameplay code 수정, baseline asset 변경, `Forward` fallback/oracle 제거, other archetype rollout 변경은 이번 close 범위가 아니다.

## Executed Commands
- `Unity.exe -batchmode -nographics -projectPath C:/Users/user/2026TeamProject_J2M -logFile C:/Users/user/2026TeamProject_J2M/TestResults/phase5-red-closure/baseline-control-targeted.log -runTests -runSynchronously -testPlatform EditMode -assemblyNames Game.Feature.Gameplay.Tests -testFilter Game.Feature.Gameplay.Tests.Unit.EnemyLogicTests.EnemyAi_WindupForwardBaseline_AttackCommittedControlProbe_IsComplete;Game.Feature.Gameplay.Tests.Unit.EnemyLogicTests.EnemyAi_WindupForwardBaseline_LockedTargetLostControlProbe_IsComplete -testResults C:/Users/user/2026TeamProject_J2M/TestResults/phase5-red-closure/baseline-control-targeted.xml`
- `Unity.exe -batchmode -nographics -projectPath C:/Users/user/2026TeamProject_J2M -logFile C:/Users/user/2026TeamProject_J2M/TestResults/phase5-red-closure/unit-runtime-targeted.log -runTests -runSynchronously -testPlatform EditMode -assemblyNames Game.Feature.Gameplay.Tests -testFilter Game.Feature.Gameplay.Tests.Unit.EnemyLogicTests.EnemyLogic_WindupRandomWalkPilot_PatrolStateWrites_OccurOnlyOnInitAndCommittedPatrolMove;Game.Feature.Gameplay.Tests.Unit.EnemyLogicTests.EnemyLogic_WindupRandomWalkPilot_DoesNotWritePatrolState_DuringChaseAttackRecover;Game.Feature.Gameplay.Tests.Unit.EnemyLogicTests.EnemyAi_WindupRandomWalkPilot_AttackWindupRecoverContract_MatchesForwardBaseline -testResults C:/Users/user/2026TeamProject_J2M/TestResults/phase5-red-closure/unit-runtime-targeted.xml`
- `Unity.exe -batchmode -nographics -projectPath C:/Users/user/2026TeamProject_J2M -logFile C:/Users/user/2026TeamProject_J2M/TestResults/phase5-red-closure/scenario-targeted.log -runTests -runSynchronously -testPlatform EditMode -assemblyNames Game.Integration.Simulation.Tests -testFilter Game.Feature.Gameplay.Tests.Scenario.EnemyAiScenarioTests.EnemyAi_WindupRandomWalkPilot_DirectLane_MatchesExactTransitionTicks;Game.Feature.Gameplay.Tests.Scenario.EnemyAiScenarioTests.EnemyAi_WindupRandomWalkPilot_OpenRoomOffset_DoesNotAdvanceAggressionEarlierThanBaseline;Game.Feature.Gameplay.Tests.Scenario.EnemyAiScenarioTests.EnemyAi_WindupRandomWalkPilot_LoseTargetDuringWindup_ReturnsHomeThenResumesPatrol;Game.Feature.Gameplay.Tests.Scenario.EnemyAiScenarioTests.EnemyAi_WindupRandomWalkPilot_PrimedSameCell_PreservesCombatThenPassiveOrdering -testResults C:/Users/user/2026TeamProject_J2M/TestResults/phase5-red-closure/scenario-targeted.xml`
- `Unity.exe -batchmode -nographics -projectPath C:/Users/user/2026TeamProject_J2M -logFile C:/Users/user/2026TeamProject_J2M/TestResults/phase5-red-closure/replay-targeted.log -runTests -runSynchronously -testPlatform EditMode -assemblyNames Game.Integration.Replay.Tests -testFilter Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_WindupRandomWalkPilot_ProducesStableHashTrace_AndBoundedPatrolDump;Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_WindupRandomWalkPilot_PatrolDump_MatchesFinalSnapshotState;Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_WindupRandomWalkPilot_DoesNotRegressForwardOrNonAttackingReplays;Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.DeterminismHash_WindupRandomWalkPilot_PatrolFootprint_IsLimitedToEnemyPatrolRuntimeState;Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_ForwardProfile_ProducesStableHashTrace_AndNoPatrolStateWrites -testResults C:/Users/user/2026TeamProject_J2M/TestResults/phase5-red-closure/replay-targeted.xml`
- historical authoring/doc targeted evidence included Phase5 WindupMelee repository profile checks that are retired in the current Enemy AI profile cleanup.

## Artifact List With Exact Dates
- `TestResults/phase5-red-closure/evidence-summary.md` (`2026-04-24`, final evidence verdict only)
- `TestResults/phase5-red-closure/baseline-control-targeted.xml` (`2026-04-24`)
- `TestResults/phase5-red-closure/baseline-control-targeted.log` (`2026-04-24`)
- `TestResults/phase5-red-closure/unit-runtime-targeted.xml` (`2026-04-24`)
- `TestResults/phase5-red-closure/unit-runtime-targeted.log` (`2026-04-24`)
- `TestResults/phase5-red-closure/scenario-targeted.xml` (`2026-04-24`)
- `TestResults/phase5-red-closure/scenario-targeted.log` (`2026-04-24`)
- `TestResults/phase5-red-closure/replay-targeted.xml` (`2026-04-24`)
- `TestResults/phase5-red-closure/replay-targeted.log` (`2026-04-24`)
- `TestResults/phase5-red-closure/unit-authoring-doc-targeted.xml` (`2026-04-24`)
- `TestResults/phase5-red-closure/unit-authoring-doc-targeted.log` (`2026-04-24`)
- `TestResults/phase5-red-closure/attackcommitted-gate-summary.txt` (`2026-04-24`)
- `TestResults/phase5-red-closure/lockedtargetlost-conditional-summary.txt` (`2026-04-24`)
- `TestResults/phase5-red-closure/patrol-write-triage-summary.txt` (`2026-04-24`)

## Result Summary
- phase 5 close gate는 same-revision targeted evidence bundle과 documentation governance lock을 기준으로 판정한다.
- `Close Retry Ready`는 evidence verdict이고, `Closed`는 README/read order/historical demotion/doc tests까지 같은 revision에서 잠긴 최종 decision wording이다.
- 현재 close는 broad/full suite closure를 뜻하지 않으며, unrelated lane red는 close blocker가 아니다.

## Reviewed Truth Sources
- `Docs/Architecture/Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md`
- `Docs/Architecture/Gameplay-EnemyPatrol-Decision-Proposal-Contract.md`
- `Docs/Architecture/Gameplay-EnemyPatrol-Phase3-Forward-Commonization.md`
- `Docs/Architecture/Gameplay-EnemyPatrol-Forward-Rollout-Gate.md`
- `Docs/Architecture/Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md`
- `Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md`
- `Docs/Testing/Bounded-Lane-Close-Template.md`
- `TestResults/phase5-red-closure/evidence-summary.md`

## Close Gate
| gate | pass 조건 | canonical evidence | blocker 제외 규칙 |
| --- | --- | --- | --- |
| bundle integrity | 필수 artifact 전부 존재, 같은 revision/실행 창으로 묶임 | `evidence-summary.md` + file inventory + XML/log/txt timestamp/revision | optional debug artifact 부재는 blocker 아님 |
| baseline control | green | `baseline-control-targeted.xml/.log` | broad lane red와 무관 |
| runtime parity | green | `unit-runtime-targeted.xml/.log` + `attackcommitted-gate-summary.txt` + `patrol-write-triage-summary.txt` | unrelated runtime suite 무관 |
| scenario parity | green | `scenario-targeted.xml/.log` + `lockedtargetlost-conditional-summary.txt` | bundle 밖 scenario red 무관 |
| replay / determinism | green | `replay-targeted.xml/.log` | broad replay red 무관 |
| authoring / doc / no-touch | green | `unit-authoring-doc-targeted.xml/.log` | broad authoring backlog 무관 |
| governance close lock | README, rollout, close execution, historical notes, doc tests가 같은 상태를 말함 | 문서 diff + doc tests | broad/full suite status는 close gate 아님 |

## Supporting Truth-Source Hierarchy
| 문서 | 분류 | 역할 |
| --- | --- | --- |
| `Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md` | active truth-source | 기본 책임 경계 유지 |
| `Gameplay-EnemyPatrol-Decision-Proposal-Contract.md` | active truth-source | no-touch contract 유지 |
| `Gameplay-EnemyPatrol-Phase3-Forward-Commonization.md` | active truth-source | `Forward` fallback/oracle 유지 근거 |
| `Gameplay-EnemyPatrol-Forward-Rollout-Gate.md` | active truth-source | forward gate / unchanged matrix 유지 |
| `Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md` | active truth-source | `WallFollow` no-touch 근거 유지 |
| `Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md` | active truth-source | final bounded rollout contract와 current non-claim |
| `Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md` | active truth-source | official close decision, gate, approve/hold, phase 6 boundary |
| `Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md` | historical supporting note | close 당시 runtime parity recovery path |
| `Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md` | historical supporting note | pre-close hardening / historical red-state separation |
| `Gameplay-EnemyPatrol-Phase5-WindupMelee-Runtime-Fix-Plan.md` | historical supporting note | bounded runtime-fix provenance |
| `Gameplay-EnemyPatrol-Phase6-JumpChaser-Readiness.md` | inactive readiness template | close 이후 별도 단계 |
| `Gameplay-EnemyPatrol-Phase6-Charge-Readiness.md` | inactive readiness template | close 이후 별도 단계 |

## Close Wording Migration
| 대상 | evidence / 문서 verdict | current role |
| --- | --- | --- |
| `Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md` | `closed` current truth | final bounded rollout contract와 explicit non-claims |
| `Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md` | historical supporting note | 당시 runtime parity recovery path |
| `Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md` | historical red-state supporting note | 당시 hard gate / red-state separation |
| `Gameplay-EnemyPatrol-Phase5-WindupMelee-Runtime-Fix-Plan.md` | historical bounded runtime-fix plan | 당시 bounded touch set provenance |
| `TestResults/phase5-red-closure/evidence-summary.md` | `Close Retry Ready` evidence verdict | final decision을 대체하지 않는 evidence summary |
| `Docs/Architecture/README.md` | active read order lock | `rollout + close execution` active, 나머지는 historical/inactive 분리 |

## Decision Outcome
| branch | trigger | status wording | next action |
| --- | --- | --- | --- |
| `Approved -> Closed` | same-revision targeted bundle green + governance close lock green | `closed` | active truth-source를 `rollout + close execution`으로 유지 |
| `Held -> Close Retry Ready 유지` | gate red, mixed revision, mixed execution window, wording drift | `close retry ready` | historical demotion / final close wording publish 보류 |

현재 outcome은 `Approved -> Closed`다. 다만 이 close는 `evidence-summary.md` verdict를 `Closed`로 바꾸지 않으며, evidence verdict와 final close decision을 분리 유지한다.

## Allowed Claims
- `WindupMelee RandomWalk pilot` phase 5 bounded rollout은 same-revision targeted evidence bundle을 기준으로 close되었다.
- close gate에는 `TestResults/phase5-red-closure/` bundle과 documentation governance lock만 사용한다.
- retired `EnemyAi_WindupMelee.asset` profile은 current repository inventory가 아니다. `EnemyBrain_WindupMelee.asset` shared brain, `Forward` fallback/oracle 유지, other archetype no-touch가 계속 current truth다.

## Explicit Non-Claims
- 이 close는 broad/full suite가 모두 closed라는 뜻이 아니다.
- unrelated broad lane red, Lane A carryover, historical backlog는 phase 5 close blocker가 아니다.
- 이 close는 `Forward` cleanup, fallback/oracle 제거, baseline asset 변경, other archetype rollout 승인, phase 6 자동 착수를 뜻하지 않는다.
- evidence verdict `Close Retry Ready`와 final decision `Closed`를 한 문구로 합치지 않는다.

## No-Touch Confirmation
- retired `EnemyAi_WindupMelee.asset` profile은 current repository inventory가 아니며, `EnemyBrain_WindupMelee.asset` shared brain은 유지
- `Forward` fallback/oracle 유지
- `NonAttacking` pilot unchanged
- `JumpChaser`, `Charge`, `WallFollow`, `TutorialPassiveContact` rollout/patrol 정책 unchanged
- proposal contract, deterministic chooser, topology rule unchanged
- stage-content canonical path, builder/result gameplay-only boundary unchanged
- broad backlog recovery와 분리 유지

## Open Functional Backlog / Handoff
- current phase 5 close 범위 안에서 추가 handoff는 없다.
- phase 6 readiness review는 inherited open backlog가 아니라 별도 decision artifact 단계다.

## Phase 6 Boundary
- `Gameplay-EnemyPatrol-Phase6-JumpChaser-Readiness.md`와 `Gameplay-EnemyPatrol-Phase6-Charge-Readiness.md`는 inactive readiness template이다.
- phase 5 close는 phase 6 readiness review의 선행 조건일 뿐이며, close 승인만으로 다음 rollout이 열리지 않는다.
- phase 6는 별도 readiness 문서, 별도 same-revision evidence bundle, 별도 decision outcome이 있어야만 시작할 수 있다.

## Open Risks
- 이 close note는 targeted bundle에만 묶여 있으므로 broad/full suite state를 설명하지 않는다.
- evidence bundle과 final decision은 의도적으로 분리돼 있으므로, future wording drift가 생기면 `README`, 이 문서, `evidence-summary.md`, doc tests를 같은 change set에서 다시 잠가야 한다.
