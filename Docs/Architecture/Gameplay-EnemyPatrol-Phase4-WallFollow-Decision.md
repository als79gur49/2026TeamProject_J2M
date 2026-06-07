# Enemy Patrol Phase 4: `WallFollow` Decision (Bounded)

이 문서는 active architecture supporting truth-source이며, entrypoint는 [README.md](./README.md)다.

## 1. 단계 4 목표 요약

phase 4의 목표는 `WallFollow`를 지금 `RandomWalk` / `Forward`와 같은 공통 proposal frame으로 옮기는 것이 아니다. 목표는 현재 `WallFollow` truth와 owner surface를 evidence-first로 고정하고, `유지` 또는 `별도 재설계 task open` 중 하나로 판정을 닫는 것이다.

- phase 4는 구현보다 판정이 우선이다.
- `WallFollow`는 기본적으로 out-of-scope active bounded strategy로 유지한 상태에서 검토를 시작한다.
- `RandomWalk` pilot, `Forward` commonization, `EnemyPatrolRuntimeState` canonicality는 흔들지 않는다.

## 2. 현재 상태와 왜 `WallFollow`를 별도 결정 단계로 다뤄야 하는지

현재 `EnemyPatrolDecisionPlanner` support matrix는 `Forward`, `RandomWalk`만 지원하고 `WallFollow`는 unsupported bounded path로 남아 있다. `WallFollow` truth는 한 레이어에 있지 않다.

- pure hand-rule direction order와 empty-space seek boundary classification은 `EnemyMovementPolicy.cs`가 소유한다.
- same-cell passive-contact hold, unsupported fallback, `BeforeAttack` rotate-only facing은 `EnemyLogic.cs`가 소유한다.
- patrol state post-move commit gate는 `MovementCommitter.cs`가 소유한다.
- authored default와 rollout contract는 `EnemyAi_WallFollower.asset`, `EnemyPatrol_WallFollow_Left.asset`, stage builder tests가 소유한다.

즉 `WallFollow`는 단순 direction chooser가 아니라 movement / no-move / facing / hold가 얽힌 active bounded strategy다. phase 4는 이 분산을 common proposal seam으로 흡수할지 결정하는 구현 단계가 아니라, 이 분산이 current bounded truth인지 future redesign cue인지 판정하는 decision 단계다.

## 3. 이번 단계 범위

### 포함

- current `WallFollow` truth를 rule-by-rule로 고정한다.
- `WallFollow` owner surface를 runtime / commit / authoring / docs / tests 기준으로 정리한다.
- `WallFollow`와 `Forward` / `RandomWalk`의 decision-frame 적합성 차이를 비교한다.
- `유지` vs `재설계` 기준표를 고정하고 실제 verdict를 하나 선택한다.
- README, supporting docs, governance tests를 phase 4 결과와 일치시킨다.

### 제외

- `WallFollow`를 proposal frame으로 즉시 이전하는 구현
- `Forward` / `RandomWalk` proposal seam 재확장 또는 rollback
- `EnemyPatrolRuntimeState`를 `WallFollow`에 즉시 적용하는 작업
- `JumpChaser`, `Charge`, `TutorialPassiveContact`, `WallFollower` 기본 patrol 정책 변경
- stage-content canonical path, builder/result gameplay-only boundary, topology rule, deterministic chooser 재설계, broad backlog recovery

## 4. `WallFollow` current truth table

| rule | observable truth | current owner | existing evidence | proposal-frame fit | phase4 note |
| --- | --- | --- | --- | --- | --- |
| seek without boundary | hand-side / hand-back diagonal / front boundary가 없으면 `Forward -> PreferredTurn -> OppositeTurn -> Back` 순서로 boundary를 찾는다 | `EnemyMovementPolicy.cs` + `EnemyLogic.ResolvePatrolFacing(...)` | `WallFollow_EmptySpace_SeeksForward_DoesNotLeftTurnLoop`, `SunWheel_EmptySpace_SeeksForward` | 낮음 | empty-space seek와 rotate-only / no-legal reason을 direction-only proposal로 표현할 수 없다 |
| pure hand-rule order | left-hand는 `Left -> Forward -> Right -> Back`, right-hand는 `Right -> Forward -> Left -> Back` 순서로 passability만 평가한다 | `EnemyMovementPolicy.cs` (`ChooseWallFollowDirection`) | `WallFollowPatrolStrategy_LeftHandRule_LeftOpenChoosesLeftBeforeForward`, `WallFollowPatrolStrategy_RightHandRule_MirrorsLeftHandOrder` | 부분 적합 | deterministic order는 표현 가능하지만 seek / no-legal reason은 빠진다 |
| no post-move scoring | 후보 이동 후 boundary 유지 / 생성 여부는 후보 순서를 재정렬하지 않는다 | `EnemyMovementPolicy.cs` | `WallFollowPatrolStrategy_LeftHandRule_HandBackDiagonalPreservesCandidateOrder`, `SunWheel_LeftHand_HandBackDiagonalPreservesCandidateOrder` | 낮음 | proposal이 단순 direction만 담으면 obsolete post-move-scored path와 구분 근거가 사라진다 |
| box boundary | `followBoxes == true`면 box도 hand-side / hand-back diagonal / front wall-follow boundary로 취급한다 | `EnemyMovementPolicy.cs` | `EnemyMovementStrategyShared_WallFollowAnchor_TreatsBoxAsAnchor` | 낮음 | wall-follow-specific boundary policy가 common layer로 새어 나간다 |
| board-edge boundary | `treatBoardEdgeAsObstacleBoundary == true`면 relevant relative board edge도 wall-follow boundary로 취급하지만 candidate order scoring에는 쓰지 않는다 | `EnemyMovementPolicy.cs` + `EnemyPatrol_WallFollow_Left.asset` | `EnemyMovementStrategyShared_WallFollowBoundaryContext_TreatsAdjacentBoardEdgeAsTrackableBoundary`, `SunWheel_BoardEdgeOnlyStraight_LeftHand_MovesForward` | 낮음 | board-edge는 state selection signal일 뿐 proposal scoring signal이 아니다 |
| unit ignored as boundary | unit / player는 wall-follow boundary context로 보지 않는다 | `EnemyMovementPolicy.cs` | `EnemyMovementStrategyShared_WallFollowAnchor_IgnoresUnitsIncludingPlayers` | 낮음 | boundary classification policy가 common layer로 새어 나간다 |
| preferred turn | turn preference는 movement candidate ordering과 rotate-only facing에 적용된다 | `EnemyMovementPolicy.cs` | `WallFollowPatrolStrategy_LeftHandRule_LeftForwardBlockedRightOpenChoosesRightNotBack`, `EnemyMovementStrategyShared_WallFollowRotateOnlyFacing_UsesTurnPreferenceSymmetry` | 부분 적합 | direction과 facing-only ordering을 같은 field 집합으로 표현할 수 없다 |
| turn-and-move | blocked forward라도 turn candidate가 있으면 same tick에 facing과 move가 함께 바뀔 수 있다 | `EnemyMovementPolicy.cs` + `EnemyLogic.ResolvePatrolFacing(...)` | `DefaultEntityLogicProvider_WallFollowerProfile_ForwardBlocked_TurnsAndMovesInSameTick` | 낮음 | movement proposal 하나만으로는 stage별 facing owner를 설명하지 못한다 |
| same-cell passive contact hold | `WallFollow` + passive contact + same-cell target이면 movement를 억제하고 same-cell damage만 남긴다 | `EnemyLogic.cs` (`ShouldHoldWallFollowForSameCellPassiveContact`) | `EnemyLogic_WallFollowPassiveContact_SameCellHold_SuppressesMovementIntent_AndStillProducesPassiveContact`, `EnemyAi_WallFollowerProfile_WithPassiveContact_SameCellDealsDamage` | 불가 | current proposal contract는 hold reason을 표현하지 않는다 |
| dead-end rotate-only | move candidate가 전혀 없으면 `BeforeAttack` stage에서 turn preference 기반 rotate-only facing이 가능하다 | `EnemyLogic.ResolvePatrolFacing(...)` + `EnemyMovementPolicy.cs` (`TryChooseWallFollowRotateOnlyFacing`) | `EnemyLogic_WallFollowBeforeAttackStage_DeadEnd_CommitsRotateOnlyFacing`, `WallFollowPatrolStrategy_AllDirectionsBlocked_RotatesInPlaceWithoutMovementIntent` | 불가 | `HasDirection == false`와 rotate-only facing을 분리하는 새 surface가 필요하다 |
| locomotion cooldown 유지 | locomotion cooldown 동안 wall-follow rule은 잠시 정지하지만, cooldown 해제 후 같은 rule ordering으로 재개된다 | `EnemyLogic.ResolveBaselineGroundLocomotion(...)` + existing wall-follow helpers | `EnemyAi_WallFollowerProfile_WithLocomotionCooldown_PreservesWallFollowRule` | 부분 적합 | cooldown owner는 proposal layer 바깥이다 |
| no patrol-state footprint | `WallFollow`는 proposal init write가 없고 initialized patrol state도 만들지 않으므로 `EnemyPatrolStateUpdated` / patrol dump footprint가 없다 | unsupported proposal matrix + `EnemyLogic.TryInitializePatrolStateFromProposal(...)` + `MovementCommitter.ResolveEnemyPatrolResolutions(...)` gate | `Replay_WallFollowerProfile_ProducesStableHashTrace_AndNoPatrolStateWrites` | 불가 | `EnemyPatrolRuntimeState`를 `WallFollow`에 즉시 적용하지 않는다 |

## 5. `WallFollow` owner surface

| surface | current owner | why not simple proposal | allowed phase4 action | future redesign cue |
| --- | --- | --- | --- | --- |
| boundary context gate | `EnemyMovementPolicy.cs` | wall / box / board-edge / unit-ignore policy가 common decision layer로 새면 wall-specific semantics가 된다 | truth 문서화, tests pin | boundary-context extraction이 필요할 때만 별도 task |
| candidate ordering | `EnemyMovementPolicy.cs` | following / acquisition / seek order는 movement passability와 boundary classification을 함께 봐야 한다 | current ordering 고정 | wall-only adapter spike가 필요할 때 분리 검토 |
| post-move boundary scoring | 없음 | destination boundary scoring은 current truth가 아니다 | obsolete path 제거 | 재도입 금지 regression guard |
| pre-movement facing | `EnemyLogic.ResolvePatrolFacing(...)` | movement intent와 별개 owner surface다 | facing owner 문서화 | facing-only boundary task |
| same-cell hold | `EnemyLogic.cs` | no-move reason이 patrol이 아니라 passive-contact와 결합된다 | hold 테스트 보강 | same-cell hold boundary task |
| `BeforeAttack` rotate-only | `EnemyLogic.ResolvePatrolFacing(...)` | `HasDirection == false`와 rotate-only facing이 분리된다 | stage owner 문서화 | rotate-only boundary task |
| movement intent build | `WallFollowPatrolStrategy` + shared helper | move-only로 줄이면 hold / rotate-only / seek classification reason이 빠진다 | unchanged 유지 | adapter fit spike |
| patrol-state write gate | `MovementCommitter.cs` | initialized patrol state가 없으면 write가 생기지 않는다 | no-footprint evidence pin | state footprint redesign이 실제로 필요할 때만 별도 검토 |
| authoring asset | `EnemyPatrol_WallFollow_Left.asset` / `WallFollowPatrolAsset.cs` | shipping archetype default를 phase 4에서 흔들면 bounded decision이 아니라 rollout 변경이 된다 | asset contract test 추가 | authored policy change는 별도 rollout task |
| stage profile binding | `EnemyAi_WallFollower.asset`, stage builder | profile drift는 phase 4 scope 밖이다 | existing stage tests 유지 | stage-content task로만 분리 |
| replay / hash / trace visibility | replay harness + deterministic hash | no patrol-state footprint와 same-tick facing/move sequencing을 함께 봐야 한다 | replay evidence 추가 | drift가 생길 때만 redesign question reopen |
| documentation governance | README + phase2/3/4 docs + doc tests | phase 4가 contract 자체를 다시 정의하면 bounded decision 문서가 아니다 | phase 4 doc / README / tests align | contract drift가 생기면 defer |

## 6. `WallFollow` vs `Forward` / `RandomWalk`

| dimension | `Forward` | `RandomWalk` | `WallFollow` | implication for phase4 |
| --- | --- | --- | --- | --- |
| state dependency | stateless | `EnemyPatrolRuntimeState` read + init hint 필요 | canonical patrol state는 없지만 stage-local hold / rotate-only / boundary context가 필요 | simple proposal fit이 가장 낮다 |
| decision shape | direction or no-direction | proposal plan + facing + init hint | movement / no-move / facing / hold / rotate-only 결합 | 단순 direction proposal로 환원하기 어렵다 |
| facing semantics | 항상 current facing | planner가 planned facing 반환 | `BeforeMovement` turn, `BeforeAttack` rotate-only 둘 다 있음 | current 5-field contract만으로 부족하다 |
| no-move semantics | blocked stop | no-direction | same-cell hold와 dead-end rotate-only가 분리됨 | no-move reason surface가 필요하다 |
| same-cell interaction | 없음 | 없음 | passive-contact hold와 결합 | proposal-only frame으로는 표현 불가 |
| authoring archetype | `WindupMelee`, `JumpChaser`, `Charge` 등 | `NonAttacking` pilot | `WallFollower` shipping archetype | default patrol policy drift를 금지해야 한다 |
| owner dispersion | 낮음 | 중간 | 높음 | commonization 이득보다 owner relocation 비용이 크다 |
| proposal-only expressibility | 예 | 예 | 아니오 | current `EnemyPatrolDecisionProposal` 5필드만으로는 부족하다 |
| trace / patrol-state footprint | stable, patrol state footprint 없음 | stable, patrol state footprint 있음 | stable, patrol state footprint 없음 | state footprint를 맞추려고 `EnemyPatrolRuntimeState`를 강제 적용하지 않는다 |
| rollout blast radius | `Forward` archetypes | bounded pilot only | `WallFollower` + passive-contact interaction | phase 4는 decision-only로 닫아야 한다 |

phase 4의 핵심 질문은 하나로 고정한다.

- 현재 `EnemyPatrolDecisionProposal` 5필드만으로 `WallFollow` truth를 표현할 수 있는가

현재 답은 `아니오`다. same-cell hold, `BeforeAttack` rotate-only, empty-space seek, no-legal-move reason은 새 field나 stage-specific planner branching 없이는 표현되지 않는다. 따라서 `WallFollow`는 phase 4 기준 simple proposal candidate 아님으로 고정한다.

## 7. `유지` vs `재설계` 판정 기준

| criterion | `유지` | `재설계 task open` | required evidence |
| --- | --- | --- | --- |
| current truth coherence | code / tests / docs로 coherent하게 문서화 가능 | code / tests / docs가 서로 충돌 | truth table + evidence tests |
| replay / hash / trace stability | stable replay와 no-patrol-state footprint 유지 | trace drift나 unexpected patrol-state footprint 발생 | replay determinism tests |
| proposal contract fit | current 5-field contract로 표현 불가 | small bounded redesign으로 표현 surface를 분리할 실익이 큼 | comparison table + owner surface |
| common layer cost | planner/common layer가 wall-specific semantics를 떠안는 비용이 큼 | bounded extraction으로 common layer 이득이 명확 | owner surface + redesign cue |
| authored drift | `WallFollower` asset/profile drift 없음 | authored policy와 runtime truth가 어긋남 | authoring tests |
| stage-local owner clarity | hold / rotate-only owner가 testable하게 고정돼 있음 | hold / rotate-only owner가 불명확 | unit / scenario evidence |

기본 verdict rule은 보수적으로 둔다.

- phase 4는 `지금 commonization`이 아니라 `지금 유지할 근거가 충분한가`를 본다.
- 재설계는 “향후 bounded redesign task를 열 가치가 evidence로 입증됐을 때만” 연다.

## 8. 결정 결과

`WallFollow` verdict는 `유지`다.

- `WallFollow`는 legacy가 아니라 active bounded strategy다.
- current truth는 code / tests / docs로 coherent하게 문서화 가능하다.
- replay / hash / trace 관점에서도 stable하고 patrol-state footprint가 없다.
- common layer로 올리려면 wall-specific boundary context, same-cell hold, rotate-only stage surface를 planner/common layer가 새로 떠안아야 한다.

phase 4 이후 canonical truth는 아래로 고정한다.

- `WallFollow`는 unsupported bounded strategy로 유지한다.
- `WallFollow`를 proposal support matrix에 추가하지 않는다.
- `EnemyPatrolRuntimeState`를 `WallFollow`에 즉시 적용하지 않는다.
- `RandomWalk` pilot과 `Forward` commonization을 흔들지 않는다.

## 9. 검증 evidence

### unit

- `WallFollowPatrolStrategy_RightHandWall_PrefersForwardWhileHandAnchorExists`
- `WallFollowPatrolStrategy_LeftHandRule_LeftOpenChoosesLeftBeforeForward`
- `WallFollowPatrolStrategy_LeftHandRule_LeftBlockedForwardOpenChoosesForward`
- `WallFollowPatrolStrategy_LeftHandRule_LeftForwardBlockedRightOpenChoosesRightNotBack`
- `WallFollowPatrolStrategy_LeftHandRule_LeftForwardRightBlockedBackOpenChoosesBack`
- `WallFollowPatrolStrategy_RightHandRule_MirrorsLeftHandOrder`
- `WallFollowPatrolStrategy_LeftHandRule_HandBackDiagonalPreservesCandidateOrder`
- `WallFollow_EmptySpace_SeeksForward_DoesNotLeftTurnLoop`
- `WallFollow_EmptySpace_RightHand_SeeksForward_DoesNotRightTurnLoop`
- `EnemyMovementStrategyShared_WallFollowAnchor_TreatsBoxAsAnchor`
- `EnemyMovementStrategyShared_WallFollowAnchor_DoesNotTreatBoardEdgeAsAnchor`
- `EnemyMovementStrategyShared_WallFollowBoundaryContext_TreatsAdjacentBoardEdgeAsTrackableBoundary`
- `EnemyMovementStrategyShared_WallFollowBoundaryContext_BoardEdgeOptOutSeeksForward`
- `WallFollowPatrolStrategy_BoardEdgeOnlyStraight_LeftHand_MovesForward`
- `WallFollowPatrolStrategy_BoardEdgeOnlyCorner_LeftHand_ChoosesRightNotBack`
- `EnemyMovementStrategyShared_WallFollowRotateOnlyFacing_UsesTurnPreferenceSymmetry`
- `EnemyLogic_WallFollowPassiveContact_SameCellHold_SuppressesMovementIntent_AndStillProducesPassiveContact`
- `EnemyLogic_WallFollowBeforeAttackStage_DeadEnd_CommitsRotateOnlyFacing`

### replay / determinism

- `Replay_WallFollowerProfile_ProducesStableHashTrace_AndNoPatrolStateWrites`

### scenario

- `EnemyAi_WallFollowerProfile_CirculatesAroundWallAcrossMultipleTicks`
- `EnemyAi_WallFollowerProfile_CirculatesAroundBoxAcrossMultipleTicks`
- `SunWheel_LeftHand_LeftOpen_ChoosesLeftBeforeForward`
- `SunWheel_LeftHand_LeftBlockedForwardOpen_ChoosesForward`
- `SunWheel_LeftHand_LeftForwardBlockedRightOpen_ChoosesRightNotBack`
- `SunWheel_LeftHand_LeftForwardRightBlockedBackOpen_ChoosesBack`
- `SunWheel_LeftHand_HandBackDiagonalPreservesCandidateOrder`
- `SunWheel_BoardEdgeOnlyStraight_LeftHand_MovesForward`
- `SunWheel_BoardEdgeOnlyCorner_LeftHand_ChoosesRightNotBack`
- `SunWheel_EmptySpace_SeeksForward`
- `EnemyAi_WallFollowerProfile_WithLocomotionCooldown_PreservesWallFollowRule`
- `WallFollowPatrolStrategy_AllDirectionsBlocked_RotatesInPlaceWithoutMovementIntent`
- `DefaultEntityLogicProvider_WallFollowerProfile_ForwardBlocked_TurnsAndMovesInSameTick`
- `EnemyAi_WallFollowerProfile_WithPassiveContact_SameCellDealsDamage`

### authoring

- `EnemyAiProfileAssets_PatrolPilotRollout_MatchesExpectedPatrolKinds`
- `EnemyPatrolAssets_WallFollowAsset_StillResolvesWallFollowKind_AndSettingsContract`
- `MechanicsShowcaseStage_BuildsWallFollowerProfileOverride`
- `StageRuntimeBuilderTests` wall follower profile assertions

### documentation governance

- `EnemyPatrolPhase4DocumentationTests`

## 10. out-of-scope / no-touch

- `WallFollow` proposal frame 이전
- `Forward` / `RandomWalk` bounded seam 재정의
- `EnemyPatrolRuntimeState`를 `WallFollow`에 맞추기 위한 state footprint 추가
- `JumpChaser`, `Charge`, `TutorialPassiveContact`, `WallFollower` 기본 patrol 정책 변경
- stage-content canonical path
- builder/result gameplay-only boundary
- topology rule
- deterministic chooser 재설계
- broad backlog recovery

## 11. 재설계 task를 여는 경우의 bounded sub-problem

현재 phase 4 verdict는 `유지`이므로 follow-up redesign task는 열지 않는다. 다만 향후 reopen이 필요하면 아래 sub-problem으로만 bounded하게 쪼갠다.

- `boundary context gate extraction`
- `facing-only / rotate-only boundary`
- `same-cell passive-contact hold boundary`
- `turn-and-move same-tick contract`
- `adapter fit spike`

## 12. rollback / defer criteria

아래 중 하나라도 발생하면 phase 4는 decision-only 문서와 tests만 남기고 defer한다.

- `WallFollow` truth를 설명하려면 proposal contract부터 바꿔야 한다는 결론이 나온다.
- 새 proposal field, 새 canonical patrol state, planner stage branching이 필요하다는 결론이 나온다.
- same-cell hold나 rotate-only owner가 testable하게 고정되지 않는다.
- authored `WallFollower` policy drift가 생긴다.
- `RandomWalk` pilot, `Forward` commonization, `EnemyPatrolRuntimeState` canonicality를 흔들지 않고는 문서를 닫을 수 없다.

## 13. 최종 성공 기준

- `WallFollow` current truth, owner surface, comparison, verdict, no-touch, rollback/defer가 한 문서로 닫힌다.
- `WallFollow`는 active bounded strategy로 유지된다는 결론이 evidence-backed로 남는다.
- `RandomWalk` pilot과 `Forward` commonization은 unchanged로 유지된다.
- phase 4 doc, README, documentation governance tests가 같은 결론을 가리킨다.

## 14. 최종 실패 기준

- truth, owner surface, verdict 중 하나라도 문서로 닫히지 않는다.
- commonization 필요성이 evidence 없이 추정으로만 남는다.
- `WallFollow` unchanged evidence 없이 문서만 추가된다.
- phase 4 변경이 `Forward` / `RandomWalk` seam이나 authored patrol policy를 흔든다.
