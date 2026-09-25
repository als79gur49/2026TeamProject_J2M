# EnemyLogic Glide 실행 책임 추출 계획

## 1. 상태와 결정

- 작성일: 2026-09-25 KST. 소스 기준: `2692bd37f35f3facb0c4bdbc240078f2a968fea0`, `worktree/ui-audio-m1-continuation`.
- 실행 상태: **P0–P4 완료, 서브 에이전트 재검토 보완 완료**. 2026-09-26 KST의 구현, 재확보한 baseline과 candidate의 66 variant·104 record 차이 0, 양측 graphics core 406/406 및 잔여 범위는 [실행 결과](./EnemyLogic-Glide-Extraction-Closeout.md)에 기록했다. 아래 §10의 계획 검토 기록은 작성 당시 이력이다.
- 계획 작성 당시 working tree는 clean이었다. 실행 세션은 사용자 README/계획/실행 프롬프트 변경과 ahead 6 commit을 보존했다.
- 상위 결정: [행동별 추출 계획](./EnemyLogic-Behavior-Extraction-Implementation-Plan.md) §1/§7/§10. Summon 다음 후보 Glide를 별도 inventory로 평가한다. Jump/Charge는 이 계획에 포함하지 않는다.
- 선택: Glide의 **pre-movement lifecycle, 시작/Active 진입 목표 선택, Windup/Recovery 억제 질의**를 `internal static EnemyGlideExecutor`로 추출한다. 일반 이동과 공격 resolve를 하나의 실행기로 모으지 않는다.
- 목표는 책임 응집과 동작 보존이다. 성능 향상, 버그 수정, 새 FSM, authoring/schema/asset migration을 함께 수행하지 않는다.

읽을 근거: [architecture entrypoint](./README.md), [canonical simulation spec](./Tick-Simulation-Canonical-Spec.md), [테스트 운영 가이드](../Testing/Gameplay-Test-Automation-Guide.md), [Summon 추출 결과](./EnemyLogic-Summon-Extraction-Closeout.md), [커밋 규칙](../../AI_GIT_COMMIT_RULES.md). Summon의 완료 증거는 Glide 검증 증거로 재사용하지 않는다.

## 2. 책임 inventory와 결합도

아래 runtime 경로는 `Assets/_Features/Gameplay/` 기준이다. 줄 번호는 소스 기준 SHA의 안내이며 실행 시 심볼을 다시 확인한다.

| 소스/심볼 | 현재 책임 | 이번 disposition |
| --- | --- | --- |
| `Gameplay_EnemyAI/Runtime/EnemyLogic.cs:331` `CommitPreMovementState` | Jump → Charge → Glide → Utility → Summon 순서와 기존 snapshot 전달 | coordinator에 유지, 같은 guard/위치에서 위임 |
| 같은 파일 `CommitGlideState` :836 | 기존 상태 조회, invalid clear, initial delay, 시작, 최종 write | 실행기로 이동 |
| `TryAdvanceGlideLifecycle` :926 | 최대 8회 연쇄 전환, Solid 회복 보류, cooldown/Ready 처리 | 실행기로 이동 |
| `ShouldSuppressMovementForGlide` :1035 | runtime 존재 + 상태 존재 + Windup/Recovery | 실행기의 순수 질의로 이동 |
| `HasUnsettledVoluntaryKinematicPose` :1077 | 시작 시 진행 중 voluntary pose 차단 | 현재 Glide 시작 전용 helper로 이동 |
| `TryResolveGlideStartLockedStep`, `TryResolveGlideActiveStartTarget`, `TryResolveGlideLockedStep`, `TryResolveGlideLockedStepTowardTarget`, `ShouldTryHorizontalGlideStepFirst` :1117–1230 | chase 전략 우선, 축 fallback, Active 전환 시 IgnoreSolid 재탐지 | 모두 실행기로 이동; 전략 자체는 기존 owner 유지 |
| `AppendGlideUpdate`, Glide overload `AreEqual`, `FormatLockedGlideStep`, `TryGetLockedGlideStep` :2185/:1108 | 현재 public semantic property 비교와 기존 update 문자열 | 실행기로 이동, 비교 필드와 출력 순서 보존 |
| `HasGlideBehavior` :831 | runtime 존재 여부 | coordinator guard 유지 가능 |
| `TryResolveUnsettledGlideKinematicTerminal`, `IsGlideKinematicStartedDuringActiveWindow`, `TryResolveGlideStepDirection` :1054–1106 | 현재 선언 및 내부 연결만 있고 terminal helper 호출자가 검색되지 않음 | 첫 slice에 그대로 남김. 실행 시 참조 재확인; dead-code 제거는 별도 의도 |
| `ResolveBaselineGroundLocomotion`, `CreateGroundLocomotionResolution`, `IsActiveGlide` :2233/:2416/:2438 | Active 목표 상실 patrol fallback, 일반 추적, cooldown 0/GlideMoveTicks 적용 | EnemyLogic에 유지 |
| `EnemyDetectionQueryOptionResolver` :2803 | Active 상태의 일반 탐지 옵션 | 기존 위치/consumer 유지 |
| `Gameplay_EnemyAI/Runtime/EnemyGlideState.cs` | 상태 carrier, 순수 `EnemyGlideQueries`, legacy phase 정규화, semantic query | 기존 공용 코드 유지 |
| `Gameplay_Loop/Runtime/TickPipeline.cs:7677` `TryMaterializeEnemyKinematicMotionInterrupt` | 유효 공격 중단 + 비치명 피해의 Active → Recovery, freeze, metadata/events | 공격 stage batch에 유지; executor로 이동하지 않음 |
| `Gameplay_BoardState/Runtime/WorldState.cs`, snapshot, commit context/batch | authoritative record 저장/삭제, 조회, 적용 | 변경 없음 |

Summon보다 결합도가 높은 이유는 시작 판단에 detection/chase 전략과 TileFeature definitions가 필요하고, Active 상태가 통과·이동 시간·탐지·공격 중단의 공통 입력이기 때문이다. 기존 순수 상태 연산은 이미 `EnemyGlideQueries`에 있으므로 새 상태 저장소나 공용 transition 추상화가 필요하지 않다. 추출 후 EnemyLogic에 남는 Glide consumer는 의도된 coordinator/일반 이동 경계이며, lifecycle 정책의 복제와 구분한다.

## 3. 제안 API와 호출 경계

새 파일: `Gameplay_EnemyAI/Runtime/EnemyGlideExecutor.cs` 및 Unity `.meta`.

| API | 입력 | 출력/조건 |
| --- | --- | --- |
| `Commit` | snapshot, `in TickInput`, `in EntityState source`, entity ID, `EnemyGlideBehaviorRuntime`, `IDetectionStrategy`, `IChaseStrategy`, common/detection/chase settings, `IReadOnlyList<TileFeatureRuntimeDefinition>`, `IPreMovementStateCommitContext`, updates | 기존 조건에서 최대 한 번의 상태 기록과 기존 순서의 update. runtime non-null은 기존 coordinator guard가 보장 |
| `ShouldSuppressMovement` | snapshot, entity ID, runtime | bool. runtime null 또는 state 없음은 false. writer/log 입력 없음 |

인자 순서는 구현 시 조정할 수 있으나 입력 책임을 확대하지 않는다. `_tileFeatureDefinitions`는 `BindTileFeatureDefinitions`로 바뀔 수 있으므로 호출 시 현재 참조를 전달한다. 설정/전략을 static cache에 보관하지 않는다. `EnemyLogic` 전체, provider/service locator, 새 per-Tick 실행기 객체, coordinator 콜백을 전달하지 않는다. 시작 target/step을 coordinator에서 미리 계산하지 않는다. 기존 short-circuit의 호출 횟수와 재탐지 시점이 보존되어야 한다.

상태 조회/write/log는 기존 `_entityId`를 전달한 인자를 사용하고 시작 kinematic pose 조회는 기존 `source.entityId`를 사용한다. 정상 provider에서는 같더라도 이 추출에서 하나로 정리하지 않는다.

호출표:

| 경로 | 보존할 호출 |
| --- | --- |
| source lookup 실패 | Utility/Summon의 기존 취소 뒤 return. Glide에 새 Cancel 호출 추가 금지 |
| topology 비참여 | Jump suspend → Utility → Summon → return. Glide Commit 미호출 |
| 정상 pre-movement | patrol init → Jump → Charge → **Glide Commit** → Utility → Summon → controllable 재확인 → pending reaction/일반 cooldown |
| `ShouldSuppressAutonomousMovementAndFacing` | Glide OR 항의 위치와 short-circuit 보존 |
| `CollectMovementIntents`, `ResolvePatrolFacing`, `TryPreparePendingEnemyBlockedReaction` | 기존 snapshot의 억제 결과 소비. 앞선 expiry/invalid clear와 억제에 의한 보류 구분 |
| 공격 중단 | attack snapshot에서 기존 pipeline batch에 Recovery 기록. pre-movement 실행기 재호출 없음 |

## 4. 보존 계약과 현재 정책

`StrongContract`는 canonical 소유권/결정성 또는 기존 행동 assertion이 보호한다. `CurrentPolicy`도 이번 refactor에서는 유지하되 영구 제품 규칙으로 승격하지 않는다. 불명확한 기대는 P1에서 근거를 확정하며 추출과 함께 수정하지 않는다.

| ID | 분류 | 보존 사항 |
| --- | --- | --- |
| C01 | StrongContract | WorldState 소유권, snapshot 관측 시점, pre-movement/attack batch의 별도 적용 순서, SurfaceCell face identity |
| C02 | StrongContract | Solid 위 만료 Active는 WantsRecover로 유지; 이동한 Tick에 pre-movement recovery를 소급하지 않음. `GliderAirborneP0CoreTests`의 관련 assertion |
| C03 | StrongContract | Windup/Recovery의 이동·자율 facing 억제, Active/Cooldown 일반 이동; Active 이동은 locked step 고정 dash가 아님. `GlideOverSolidTests` |
| C04 | StrongContract | Active Solid 우회와 TileFeature/reservation/board edge/topology/settlement 판정의 구분; 일반 이동 owner 유지 |
| C05 | StrongContract | sequence/timing/locked metadata, replay/hash/ordered updates·presentation 입력 보존. legacy enum 값과 저장 schema 유지 |
| C06 | CurrentPolicy | lookup/topology early return은 Glide 기록을 새로 지우거나 종료 Tick을 연장하지 않음 |
| C07 | CurrentPolicy | Commit 내부 HP≤0/marked/Dead/non-Occupying이면 기존 state가 있을 때 ClearDead 기록; state 부재는 no-op. 외부 guard 때문에 실제 도달 가능한 경로와 구분 |
| C08 | CurrentPolicy | lifecycle 진행 → initial-delay 감소 → 시작 판단 → 시작 직후 lifecycle 진행 순서. delay가 0이 된 같은 호출에서 시작 가능. 설정 delay 0이면 초기화 record를 강제로 생성하지 않음 |
| C09 | CurrentPolicy | 시작은 Chase이며 unsettled voluntary pose가 없어야 함. 일반 탐지와 chase step 우선, 실패 시 같은 face/desired distance/축 우선 fallback |
| C10 | CurrentPolicy | Windup 종료 시 IgnoreSolid로 fresh target/step 재선택. 실패해도 Active 진입하며 기존 locked step/target을 유지하는 BeginActive semantics |
| C11 | StrongContract | cooldown 0이라도 LastExitedTick으로 같은 Tick 재시작 금지. 기존 lifecycle assertion 재사용 |
| C12 | CurrentPolicy | cooldown 종료 후 non-Chase는 Ready clear, Chase는 CanStart에서 cooldown 상태로 재시작 가능. 최대 8회 전환 loop, 정확한 exclusive 경계 유지 |
| C13 | StrongContract | 공격 중단의 비치명 Active → Recovery와 kinematic freeze/metadata는 기존 attack owner. 치명/비대상 경로와 구분 |
| C14 | CurrentPolicy | 억제 query의 runtime-null/state-absent false, write 없음; snapshot-null 허용 계약을 새로 추가하지 않음. pending reaction 소비는 기존 snapshot 및 선행 guard 사용 |

`AreEqual`은 현재 public semantic property를 비교한다. raw legacy serialized field까지 비교하도록 확대하지 않는다. 축 동률 fallback도 현재 식을 그대로 보존하고 일반적인 facing 우선 규칙으로 바꾸지 않는다.

## 5. 테스트 inventory와 필수 case matrix

기존 fixture 경로는 `Gameplay_Tests/EditMode/` 기준이다. 기존 테스트를 먼저 assertion 단위로 매핑하고 없는 경우에만 characterization을 보강한다. 이름 검색만으로 coverage 완료를 선언하지 않는다.

| 기존 fixture | 활용 범위 |
| --- | --- |
| `Unit/GlideOverSolidTests.cs` (Extended) | compile/timing, initial delay, full lifecycle/zero cooldown, 억제/facing, 탐지, fallback, 이동 시간, topology/legality/kinematic 경계 |
| `Core/GliderAirborneP0CoreTests.cs` | Solid 만료 유지, 비Solid 이동 다음 Tick 회복, face-aware 회복, topology churn, 억제, box overlap |
| `Core/EnemyGlideStateMigrationCoreTests.cs` | legacy LandingPending → Active+WantsRecover, enum/private compatibility |
| `Core/PlayerFlipActiveGlideLandingCoreTests.cs` | Active만 적용되는 Flip landing 예외와 인접 정책 |
| `Core/EnemyKinematicContinuationBlockedReactionCoreTests.cs` | 일반 pending reaction 회귀. Glide 결합 coverage는 별도 확인/보강 |
| `Unit/WorldSnapshotAndPresentationTests.cs`, `EnemyAiProfileAssetContractTests.cs` | Glide phase 신호/clear와 production profile/behavior owner. 해당 Glide test를 선별 |
| `Scenario/EnemyAiScenarioTests.cs`의 `GlideActive_*` | 비치명/치명 kinematic hit, Recovery/Interrupted pose, signal 및 다음 Tick cleanup |
| `Replay/GliderAirborneP1ReplayTests.cs` | 동일 구현 반복 replay 결정성. 별도 `Game.Integration.Replay.Tests` assembly이므로 integration-replay lane 사용 |

기존 assertion 재사용 기준:

- G01: `EnemyLogic_GlideInitialDelay_BlocksFirstStartUntilDelayCompletes`, `EnemyLogic_GlideStartsExpiresAndBlocksSameTickRestartWhenCooldownIsZero`, `EnemyLogic_GlideRunsFullNonZeroPhaseLifecycle`.
- G02: `EnemyLogic_WindupAndRecoverySuppressGroundMovementIntent`, `EnemyLogic_GlideSuppression_TargetLostToPatrol_PreservesAutonomousPatrolFacing`.
- G05/G06: `Glider_WantsRecover_MovesFromSolidToNonSolid_DoesNotRecoverInSameMovementTick`, `Glider_WantsRecover_UsesSurfaceCellNotPlanarCellForSolidGateAfterTopologyChange`, `Glider_TopologyChurnAcrossWindupActiveExpiredRecover_PreservesPhaseAndDeterminism`.
- G09: `GlideActive_Kinematic_HitNonlethal_CleansUpWithoutStaleHover`와 같은 fixture의 치명/default death cleanup 사례. 기존 비치명 test는 Interrupted pose/Recovery/interrupt record/track/다음 Tick cleanup을 assert한다. 이벤트 순서와 타이머는 공통 capture로 보완한다.
- G04의 `Glider_Active_TargetLost_ForMultipleTicks_UsesFallbackWithoutClearingActive`는 Active 유지와 Snap 부재를 검사하지만 fallback 이동/방향의 충분한 oracle은 아니다. 실제 intent·좌표·facing assertion을 추가하거나 다른 기존 assertion에 매핑한다.
- G03의 전환 순간 재탐지 실패 locked 값, G07 도달성, G08 Glide와 pending reaction 결합, G10 복수 source 격리는 새 characterization 우선 후보다.

새 공통 characterization은 기존 public Logic/pipeline seam에서 실행한다. 제안 fixture명 `EnemyGlideExtractionCharacterizationTests`; baseline에서 새 executor API를 참조하지 않는다. 다음 case ID별 variant와 Tick/호출의 입력, 실제 assertion 위치, expected record ID, 양측 capture를 P1 추적표로 연결한다.

새 runtime/pipeline tests는 Integration으로 분류하고 구조-only tests는 Infrastructure로 분리한다. 기존 `Core` 이름이나 category를 보고 새 pipeline tests를 Core에 추가하지 않는다. 새 fixture의 실제 assembly에 맞춘 runner lane을 P0에서 확정한다.

| ID | 필수 variant와 관측 |
| --- | --- |
| G01 | initial delay 0/1/2; Patrol에서 감소 후 Chase 진입; lifecycle의 Windup/Active/Recovery/Cooldown 종료 전·당일·다음 Tick; 허용되는 zero windup/recovery/cooldown 조합. state/sequence/로그 순서/같은 Tick 재시작 금지 |
| G02 | Windup/Recovery 각각 이동·facing 억제와 첫 해제; Active/Cooldown 비억제; module 없음/state 없음; 반복 query가 snapshot/write/로그를 바꾸지 않음 |
| G03 | 시작 일반 탐지 vs Active IgnoreSolid; Windup 중 목표 변경/소실; 재탐지 성공/실패의 locked 값; chase intent 성공/실패 및 fallback의 face·desired distance·각 axis priority/tie |
| G04 | unsettled voluntary pose 시작 차단; settled/다른 mode와 구분; Active 진행 중 일반 이동 시간·회전·목표 소실 patrol fallback. locked step과 다른 실제 이동 허용 |
| G05 | Solid 위 Active 만료와 이미 WantsRecover인 다음 Tick들; 비Solid 이동 Tick과 다음 pre-movement; 같은 planar 좌표의 다른 face; 기존 Core 테스트 재사용 |
| G06 | Windup/Active/만료 Active/Recovery 중 topology 비참여 및 재개; 신규 state 없는 비참여. 비참여 중 저장된 absolute deadline은 불변이며, 재개 후 BeginActive/BeginRecovery 등 전환으로 생성되는 새 deadline은 현재 재개 Tick 기준. 양쪽 값을 구분해 기록 |
| G07 | lookup 거부/실제 부재, HP0, markedForDeath, Dead, detached × state 없음/있음. coordinator 도달 경로를 먼저 확인. WorldState 제거가 state도 지우는 조합은 도달 불가 근거 기록; candidate 전용 API로 baseline 사례를 대체하지 않음 |
| G08 | 충분한 expiry·일치하는 mode/source·settled pose·active attack 없음으로 선행 guard 통과시킨 pending reaction: Patrol/Chase × Windup/Recovery/첫 해제. pending 유지/소비, cooldown, facing/chase, ordered event |
| G09 | attack의 Glide kinematic interrupt 비치명/치명/조건 불충족; interrupted pose, Recovery write 유무·metadata·이벤트·다음 Tick 상태. 위 기존 공격 사례와 `GlideActive_Kinematic_ContinuationDoesNotTreatArbitraryVoluntaryStateAsGlide` 재사용; 적용불가 variant의 미충족 assertion만 보강 |
| G10 | 동일 runtime을 공유하는 두 source의 서로 다른 phase/delay/sequence, 입력 entity 순서 2가지. 상태 격리 및 canonical event/hash 순서 |
| G11 | 신규 공통 `ProductionGlideProfile_CompiledProviderPipelinePreservesLifecycleAndSignals`(제안명): 실제 `EnemyAi_GlideChaser.asset`을 로드·compile하고 provider/pipeline을 통해 시작→Active→Recovery와 신호를 관측. production 튜닝을 새 golden으로 고정하지 않고 로드된 timing과 fixture Tick의 관계를 검사. 기존 `MigratedGlide_ProfileHasBehaviorOwnerAndNoLegacyMovementSkillCapability`는 별도 asset guard이며 합성 profile smoke로 이 case를 대체하지 않음 |

공통 비교에 모든 인접 legality 조합의 새 capture를 만들 필요는 없다. G01–G11은 필수 관측 표이며 기존 인접 회귀 세트와 역할을 구분한다. 도달 불가/authoring 불허 조합은 코드 근거로 제외하고, 단순 fixture 부족을 제외 사유로 쓰지 않는다.

## 6. 비교 oracle 및 구조 검증

1. baseline은 위 SHA의 Glide production에 공통 test/helper patch만 적용한다. 다른 ahead commit과 사용자 변경은 보존한다. baseline source를 교체해야 하면 exact 파일 백업/hash 및 복구 절차를 먼저 마련하고 광범위 reset/clean을 사용하지 않는다.
2. 양측 공통 test/helper/input/runner/config identity를 동일하게 고정한다. 공통 harness를 바꾸면 원래 production에서 baseline을 다시 수집한다. 새 executor 직접 계약과 새 파일을 요구하는 구조 guard는 candidate 전용이다.
3. case/variant/record ID, Tick/호출, source 존재, 전체 state fields, entity pose/facing/cooldown, pending reaction, kinematic state, ordered write/update/event, presentation, hash/trace를 캡처한다. pipeline과 직접 Logic context 사례를 구분하며 직접 context는 제안 write와 적용 전 snapshot을 따로 남긴다. 관측 불가능한 값은 이유와 함께 명시한다.
4. 비교 전 expected case/record/schema/선택 fullname의 누락·중복·추가를 검사한다. 로그/게임플레이 값의 정렬·정규화로 차이를 지우지 않는다. 시각·출력 경로 등 실행 metadata만 비교 대상 밖에 둔다. no-op도 명시적 빈 output record로 기록한다.
5. 기존 hash에 Glide 필드가 모두 들어간다고 가정하지 않는다. 명시적 상태 캡처와 exact assertion을 함께 사용한다. 같은 구현의 2회 결정성 일치는 전후 동등성의 대체가 아니다.
6. 의미 있는 경계 하나(예: Active 재탐지 옵션 또는 Solid 회복 gate)의 임시 mutation 검출과 복구 후 green을 확인한다. comparator는 누락 record/state field/order 변이 거부를 확인한다. framework 확장은 하지 않는다.
7. 구조 검토는 실제 위임, phase/목표 정책 중복 제거, static 가변 상태/역참조/직접 world write/표현 호출 부재를 확인한다. source-file guard는 새 파일까지 검사해 coverage 감소를 막는다. 줄 수나 메서드 이름만으로 완료하지 않는다.

P0에서 baseline의 전체 production tree identity를 동결한다. 계획 기준 SHA 이후 production 변경이 있으면 이를 보존한 현재 tree를 새 기준으로 명시하고 이 계획의 기준 SHA/manifest를 갱신한 뒤 P1을 시작한다. baseline/candidate 간 허용 production 차이는 `Gameplay_EnemyAI/Runtime/EnemyLogic.cs`와 신규 `EnemyGlideExecutor.cs` 및 `.meta`뿐이며, 그 밖의 production 파일은 동일 content hash여야 한다. 테스트/helper/guard 차이는 공통 또는 candidate 전용으로 따로 기록한다. 부분 source 복원 시에도 이 전체 identity 조건을 확인한다.

## 7. 실행 단계와 gate

| 단계 | 작업 | 종료 조건 |
| --- | --- | --- |
| P0 | 현재 SHA/diff, 위 inventory와 도달성, G09 기존 assertion 및 G11 신규 case 배치, runner/hook/필터/출력 경로, manifest 준비 | source identity와 필수 case·선택 목록·환경 확정. 계획 시점의 미확정 mapping 해소 |
| P1 | 공통 characterization 보강, 기존 production targeted/core, capture 및 검출력 확인 | 모든 필수 variant가 실제 assertion/capture에 연결되고 green. 설명되지 않은 touched failure 없음 |
| P2 | executor와 `.meta`, helper 이동, 두 API 위임, 파일 기반 guard 보완 | 원래 호출 순서/입력/정책 보존, focused tests 및 구조 검토 통과 |
| P3 | 동일 공통 세트 재실행/전후 비교, candidate 전용 계약·guard, core | capture 완전성 및 차이 0; 필수 선택 테스트 실제 pass; skip/0개를 pass로 계산하지 않음 |
| P4 | 실행/미실행/실패와 source identity/evidence, 잔류 책임, 재검토 기록 | Glide slice 완료 판정. Jump/Charge 자동 착수 없음 |

P2의 production 범위는 EnemyLogic/new executor 및 필요한 검사 보완이다. EnemyGlideState/WorldState/TickPipeline/compiler/profile/asset production 변경이 필요해지면 단순 추출 전제가 깨진 이유를 먼저 분류하고 별도 변경 의도로 계획을 갱신한다. baseline과 다른 기대값을 즉석에서 채택하지 않는다.

## 8. 실행 명령과 저장 정책

모든 lane은 검증 대상 worktree에서 `./run_tests.sh`로 순차 실행한다. 신규 worktree가 필요하면 `j2m-worktree-add`와 `/mnt/d/J2M/worktrees`, D 여유 ≥30 GiB, resolved project path 및 private Library를 확인한다. 기존 C legacy worktree는 이동하지 않는다. storage compliance는 `j2m-worktree-audit`; C 여유 <10 GiB는 알린다.

각 runner 호출마다 `/mnt/d/J2M/evidence/enemy-glide-extraction/<unique-run>` 아래 새 root를 만들고 `CODEX_VALIDATION_ROOT`, `TEST_RESULTS_ROOT`, `TEST_LOG_ROOT`, `CAPTURE_ROOT`를 그 하위로 명시한다. `PLAYER_BUILD_ROOT`는 `/mnt/d/J2M/builds/enemy-glide-extraction/<unique-run>`이다. `--print-config`와 `--dry-run`으로 project/output 경로를 확인하고 manifest에 명령·시각·exit code·HEAD·staged/unstaged patch·신규 파일 내용/hash·input/config hash·선택 fullname/result·XML/log/capture hash를 기록한다. 단순 `git diff`만으로 identity 완료 처리하지 않는다.

아래는 **P0에서 위 환경 및 manifest wrapper를 준비한 뒤 실행할 lane 목록**이다. 현재 실행 결과가 아니다.

```bash
./run_tests.sh --print-config
./run_tests.sh --dry-run core
./run_tests.sh full --filter 'GlideOverSolidTests,GliderAirborneP0CoreTests,EnemyGlideStateMigrationCoreTests,PlayerFlipActiveGlideLandingCoreTests,EnemyKinematicContinuationBlockedReactionCoreTests'
./run_tests.sh full --filter 'EnemyAiScenarioTests.GlideActive_,EnemyAiScenarioTests.GlideCooldown_'
./run_tests.sh full --filter 'EnemyAiProfileAssetContractTests.MigratedGlide_ProfileHasBehaviorOwnerAndNoLegacyMovementSkillCapability,WorldSnapshotAndPresentationTests.GlidePresentation_'
./run_tests.sh --integration-replay --filter GliderAirborneP1ReplayTests
./run_tests.sh core
# 아래 최종 gate는 §8의 D 출력 및 terminal evidence 환경 설정 후 실행
UNITY_GRAPHICS=1 ./run_tests.sh core
```

- 위 G09/replay/asset/presentation 명령은 필수 공통 세트다. G09 filter에서 비치명·치명·default death·임의 voluntary continuation의 네 fullname이 실제 선택됐는지 확인한다. G11 신규 case와 새 공통 characterization도 baseline부터 실제 assembly의 lane으로 실행한다. 실제 선택 fullname을 양측 비교한다.
- guide의 core feature gate 포함 설명과 달리 현재 runner는 `core-feature-gate`를 별도 lane으로 둔다. category만으로 포함 여부를 추정하지 않고 실제 assembly/runner/XML을 확인한다. replay의 Core category도 `core` 포함을 의미하지 않는다.
- `core --filter`는 Extended를 포함시키지 않으므로 상세 Glide 검증은 `full --filter`로 수행한다. fixture가 EditMode 전용인 경우 PlayMode 0개는 별도 기록하되 필수 개별 test가 0개/skip이면 미검증이다.
- candidate 전용 제안 fixture `EnemyGlideExecutorContractTests`와 `GameplayCameraShakeMixerFoundationArchitectureTests` 및 실제 수정한 guard를 `full --filter`로 실행한다. 직접 API 검사는 source 격리/무부작용/의존성 호출 등 실제 의미가 있을 때 추가한다.
- core의 graphics skip은 통과로 합산하지 않는다. Glide focused EditMode 검증 자체는 graphics를 요구하지 않는다. 이 계획은 Summon 후속에서 확인한 운영 방식과 맞춰 baseline/candidate 양측 **graphics-enabled core 전체 선택 목록 pass**를 별도의 엄격한 회귀 gate로 선택한다. `UNITY_GRAPHICS=1 ./run_tests.sh core`에 `TERMINAL_IRIS_QUALITY_OUTPUT_DIR`(해당 D run 하위), `TERMINAL_IRIS_EVIDENCE_BUNDLE_ID`, `TERMINAL_IRIS_EVIDENCE_RUN_ID`를 설정하고 dry-run으로 전달을 확인한다. 필수 값은 현재 off-center test를 P0에서 확인한다. 실패/skip이면 core gate 미완료로 기록하되 Glide focused 결과와 원인 분류를 분리한다.
- Windows exporter가 WSL 환경변수를 모두 상속한다고 가정하지 않는다. 실제 출력 경로를 확인하고 새 파일만 hash와 함께 D evidence에 보관한다. 기존 사용자 파일은 이동하지 않는다.
- baseline/candidate는 서로 다른 source identity의 비교다. candidate의 최종 공통·전용·core 결과는 같은 production/test/runner 내용에 묶는다. 최종 변경 이후 필요한 검사를 갱신한다.
- commit 요청이 뒤따르면 해당 skill을 적용하고 hook도 새 D root를 상속하도록 별도 환경을 설정한다. 이 요청에는 commit/push/PR 작업이 없다.

기본 미실행 범위: broad unfiltered full, UI, Player build, 성능, 수동 시각 검증. 이번 runtime 추출이 해당 asset/UI/성능 계약을 바꾸지 않기 때문이다. 실제 실패나 새로운 위험이 발견되면 필요한 lane만 추가한다. 과거 full baseline red와 현재 touched regression을 구분한다.

## 9. 실패 처리와 완료 판정

- baseline 관련 failure는 원인·정책/오류·기존 증거를 분류할 때까지 P2에 진입하지 않는다. 환경/timeout/XML 부재는 assertion failure와 구분한다.
- candidate 차이는 첫 state/write/event 차이의 snapshot 및 호출 순서부터 조사한다. 기대값 재생성이나 schema 축소로 숨기지 않는다.
- 복구는 자기 slice의 파일만 exact backup과 대조한다. 사용자 변경·ahead commit·원래 실패 artifact를 보존한다.
- 완료에는 G01–G11의 필수 variant mapping, 공통 capture 완전성/차이 0, candidate 계약/구조 검사, 같은 candidate core gate, 구현/미실행/남은 위험 기록이 필요하다.
- 이 완료 판정은 Glide 추출에 한정한다. project-wide/full regression 또는 모든 행동 조합 검증을 의미하지 않는다.

## 10. 계획 재검토와 이번 실행 기록

- `glide_boundary_review`: 현재 소스 inventory 조사와 초안 재검토 완료. 추가 P1/P2 차단 문제 없음; P0/P1 착수 가능, P2는 baseline gate 이후라는 판단.
- `glide_test_review`: 실제 assertion/assembly/runner와 초안 재검토 완료. 전후 비교 방식은 실행 가능하며 아래 보완 사항 제시.

| ID | 지적 | 반영 |
| --- | --- | --- |
| R01 | 미사용 terminal helper를 실사용 회복 경로로 오인할 위험 | §2에서 3개 helper 잔류/별도 정리 명시 |
| R02 | semantic 비교/ID별 사용/delay 0 정책 정밀도 | §2–4에서 raw legacy 비교 확대 금지, ID 사용 구분, 초기화 record 비강제 명시 |
| R03 | category만으로 core coverage 추정 시 replay 누락 | §5/§8에 별도 integration-replay 명령 및 runner/guide drift 기록 |
| R04 | 기존 fallback·blocked reaction test의 assertion 한계 | §5에 확인된 coverage와 G04/G08 신규 보강 구분 |
| R05 | 공격 중단 기존 coverage 재사용 필요 | §5/§8에 기존 nonlethal/lethal/default/continuation과 타이머·이벤트 capture 보완 명시 |
| R06 | 합성 profile smoke가 production asset 연결을 증명하지 않음 | G11 신규 production profile→compile→provider→pipeline 사례와 별도 asset guard 지정 |
| R07 | graphics core 요구와 Glide focused 기술 요건 혼동 | §8에서 강화 회귀 gate 선택과 focused 결과 구분 |

계획 작성 당시 변경은 계획과 index 문서뿐이었고 Unity/core/full/UI를 실행하지 않았다. 이 문단은 당시 검토 이력이며 이후 P0–P4의 실제 실행 상태와 결과는 [closeout](./EnemyLogic-Glide-Extraction-Closeout.md)을 따른다.

최종 보완본을 `glide_test_review`가 다시 확인하여 G09/replay/G04/G08/G11/graphics 구분 지적 해소와 추가 차단 문제 없음을 확인했다. 문서 검증은 `git diff --check`, 신규 계획의 상대 링크 6개 및 README 연결 확인을 통과했다. 신규 문서는 untracked이므로 일반 `git diff --stat` 집계와 구분해 확인했다.

### 추가 독립 모순·충돌 검토

사용자의 추가 요청으로 `glide_consistency_audit`, `glide_runtime_conflicts`, `glide_validation_conflicts`가 각각 상위/내부 정합성, 실제 runtime 계약, test/assembly/runner를 읽기 전용으로 검토했다. 확정된 차단 모순은 없었으며 다음 낮은 우선도 명료화 권고를 반영했다.

| ID | 근거와 지적 | 반영 |
| --- | --- | --- |
| R08 | `EnemyLogic.ShouldSuppressMovementForGlide`: runtime null과 snapshot null의 계약을 혼동할 수 있음 | C14를 runtime-null false로 한정, snapshot-null 계약 추가 금지 |
| R09 | `TryAdvanceGlideLifecycle`의 BeginActive/BeginRecovery는 현재 Tick 사용 | G06에서 비참여 중 기존 deadline 불변과 복귀 Tick 기준 새 phase deadline 생성 구분 |
| R10 | baseline의 “Glide production” 표현에 파일 집합·후속 revision 처리 불명확 | §6에 전체 production freeze, 후속 변경 보존/기준 갱신, 허용 production diff와 나머지 hash 동일성 명시 |
| R11 | 기본 core 명령만 보면 강화 graphics gate 실행을 누락할 수 있음 | §8 명령표에 필수 환경 준비 후 graphics core 실행 추가 |

`full --filter`의 Scenario 선택, Integration assembly의 internal 접근 권한, G09 네 실제 테스트, replay 별도 lane, terminal graphics 인자 전달은 소스와 일치함을 확인했다. 이 계획 작성 당시 P0/P1의 도달성/세부 assertion/환경·baseline 검증은 실행 전이었으며 이 추가 검토에서도 Unity를 실행하지 않았다. 이후 실행 증거는 [closeout](./EnemyLogic-Glide-Extraction-Closeout.md)에 있다.
