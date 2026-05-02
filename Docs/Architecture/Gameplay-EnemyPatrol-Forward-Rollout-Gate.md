# Enemy Patrol Forward Rollout Gate

이 문서는 active architecture supporting truth-source이며, entrypoint는 [README.md](./README.md)다.

## 1. Readiness Matrix

| gate | green criteria | evidence |
| --- | --- | --- |
| `Forward` stateless | `EnemyPatrolRuntimeState` read/write 불필요 | `EnemyPatrolDecisionPlanner_Forward_*` |
| same-facing invariant | `PlannedFacing == source.facing` | `EnemyPatrolDecisionPlanner_Forward_*` |
| no-init invariant | `ShouldInitializeState == false` | `EnemyPatrolDecisionPlanner_Forward_*` |
| no topology candidate | topology-changing step이 `CandidateMask` / `PlannedDirection`에 없음 | `EnemyPatrolDecisionPlanner_Forward_Boundary_ExcludesTopologyChangeCandidate` |
| special-case reduction | `EnemyLogic` direct kind branch 제거 | `EnemyPatrolSourceGovernance_EnemyLogic_DoesNotDirectlyBranchOnForwardOrRandomWalkOutsideProposalSeam` |

## 2. Quantitative Unchanged Matrix

phase 3 acceptance는 정성 문구가 아니라 아래 quantitative unchanged matrix로 판정한다.

| invariant | green criteria | test |
| --- | --- | --- |
| `Forward baseline unchanged` | open forward = 정확히 1칸 전진 | `EnemyPatrolDecisionPlanner_Forward_OpenForward_ReturnsForwardProposal` |
| `Forward baseline unchanged` | blocked stop = movement intent 없음 | `EnemyPatrolDecisionPlanner_Forward_BlockedStop_ReturnsNoDirection_SameFacing_NoInit` |
| `Forward baseline unchanged` | blocked backward = 정확히 1칸 후진 | `EnemyPatrolDecisionPlanner_Forward_BlockedBackward_ReturnsOppositeDirection_SameFacing_NoInit` |
| `Forward baseline unchanged` | canonical fixtures에서 legacy strategy와 proposal path 결과 동일 | `EnemyLogic_ForwardProposalPath_MatchesLegacyForwardStrategy_OnCanonicalFixtures` |
| `RandomWalk unchanged` | current kinematic replay hash / trace / patrol dump deterministic | `Replay_EnemyAiKinematicScenario_DeterministicCanonicalState` |
| patrol state init ownership | proposal이 요청할 때만 pre-movement init write 발생 | `EnemyLogic_PatrolDecisionProposal_InitializesState_OnlyWhenProposalRequestsIt` |
| `Forward` no patrol-state footprint | replay/trace에 새 `EnemyPatrolStateUpdated` 없음 | `Replay_ForwardProfile_ProducesStableHashTrace_AndNoPatrolStateWrites`, `DeterminismHash_ForwardProposalPath_DoesNotCreateEnemyPatrolStateFootprint` |
| blocked stop unchanged | same-cell stop + no unexpected facing write | `EnemyAi_ForwardBlockedStop_RemainsInPlace_WithoutUnexpectedFacingWrite`, `Replay_ForwardProfile_BlockedStop_ProducesStableNoMove_NoPatrolTrace` |
| patrol -> chase timing unchanged | current kinematic commit 이후 `TargetSensed`, `TargetInRange`, `AttackCommitted`, `LockedTargetLost`, `RecoverTick` contract 유지 | `EnemyAi_KinematicPatrolChaseAttackRecover_CurrentContract`, `EnemyAi_WindupProfile_LosingLockedTarget_CancelsActionAndFallsBackToPatrol` |
| random-walk feel unchanged | patrol state는 committed kinematic move 기준으로만 갱신 | `EnemyAi_RandomWalk_PatrolStateUpdatesOnlyOnCommittedKinematicMove` |

## 3. Post-Phase Decision Matrix

phase 3 green 후 immediate status는 아래로 고정한다.

| item | phase 3 post-state |
| --- | --- |
| `Forward` primary decision path | proposal-backed path |
| `ForwardPatrolStrategy` | 유지. parity oracle / rollback seam / authored semantics reference |
| default archetype rollout | 자동 오픈 금지 |
| `WallFollow` | 별도 bounded task 유지 |

## 4. Future Cleanup Gate

`ForwardPatrolStrategy` direct runtime cleanup은 별도 bounded task로만 연다.

prerequisite:

- parity suite green
- source governance green
- proposal contract drift 없음
- 다른 runtime caller 없음

## 5. Future Archetype Rollout Gate

- `JumpChaser`, `Charge`: movement-skill interaction matrix + profile-specific scenario parity 없으면 금지
- `TutorialPassiveContact`: Stationary same-cell semantics가 proposal frame 밖이라 금지
- `WallFollower`: `WallFollow` bounded task 전까지 금지

확장 rule:

- 새 strategy를 proposal support matrix에 추가하려면 새 canonical patrol state가 필요 없어야 한다.
- `EnemyLogic` 새 patrol-kind branch가 생기면 안 된다.
- profile별 quantitative unchanged suite를 새로 pinned 해야 한다.
