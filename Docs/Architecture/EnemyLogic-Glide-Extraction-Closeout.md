# EnemyLogic Glide 실행 책임 추출 결과

## 판정과 경계

2026-09-26 KST에 [Glide 실행 계획](./EnemyLogic-Glide-Extraction-Implementation-Plan.md)의 P0–P4를 실행했다. §9의 Glide slice gate는 충족했다. 이는 선별된 Glide 계약과 graphics core의 전후 동등성 판정이며, broad unfiltered full 회귀 완료 판정은 아니다. 검증 기준 HEAD는 계획의 `2692bd37f35f3facb0c4bdbc240078f2a968fea0`였고, 당시 branch는 `worktree/ui-audio-m1-continuation`의 ahead 6 상태였다. 시작 시 존재한 README/계획/실행 프롬프트 변경은 보존했다. 검증 후 동일한 source/test 내용을 test·refactor·docs 세 의도로 커밋했으며 push, PR, merge는 하지 않았다.

`internal static EnemyGlideExecutor`는 기존 `CommitGlideState`, 최대 8회 `TryAdvanceGlideLifecycle`, 시작/Active 진입 탐지와 locked step 선택, `ShouldSuppressMovementForGlide`, 그 전용 pose·비교·로그 helper를 소유한다. `EnemyLogic`은 기존 guard와 patrol init → Jump → Charge → Glide → Utility → Summon 순서에서 `Commit`을 호출하고, 같은 OR 위치에서 `ShouldSuppressMovement`를 호출한다. 전달되는 TileFeature 정의는 그 호출의 `_tileFeatureDefinitions`다. source pose 조회는 `source.entityId`, 상태 조회·write·update는 `_entityId`를 유지한다. 실행기에는 지속 가변 상태, cache, coordinator 역참조가 없다.

일반 이동·fallback·이동 시간 및 Active의 일반 탐지 옵션은 `EnemyLogic`에 남았다. 공격 stage의 비치명 interrupt→Recovery, 치명 정리, 타이머·metadata·event는 `TickPipeline` owner에 남았다. `EnemyGlideState`/WorldState/schema/compiler/profile/asset은 변경하지 않았다. 미사용 terminal helper 세 개도 `EnemyLogic`에 남겼다. Jump/Charge 추출, dead-code 정리, 범용 FSM, 최적화, 밸런스 변경은 포함하지 않았다.

## identity와 실행 환경

전체 production 해시 비교의 기준은 [baseline freeze](/mnt/d/J2M/evidence/enemy-glide-extraction/review-baseline-freeze.json), 결과는 [candidate freeze](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-freeze.json)와 [허용 차이 검사](/mnt/d/J2M/evidence/enemy-glide-extraction/review-production-identity-report.json)다. 추적 production 15,303개 중 15,302개가 같은 SHA-256이었다. 다른 추적 파일은 `EnemyLogic.cs` 하나뿐이며 `08bd4b5b…` → `26443c7f…`다. 신규 production은 `EnemyGlideExecutor.cs` (`6448579b…`)와 `.meta` (`7749779e…`) 두 파일뿐이다. 공통 capture helper/fixture 해시는 양측 동일하고, candidate 전용 테스트 네 파일과 파일 기반 guard 한 파일은 별도로 분류했다. 양측의 HEAD·runner·profile asset·공통 test/helper/input hash가 일치한다. [최종 candidate 일곱 실행 identity](/mnt/d/J2M/evidence/enemy-glide-extraction/review-final-candidate-identity.json)의 source/test/runner 및 전용 검사 30개 SHA도 모두 같다. 추출 외 production 차이는 0개다.

`j2m-worktree-audit`는 [PASS](/mnt/d/J2M/evidence/enemy-glide-extraction/worktree-audit.txt)였다. 기존 C legacy worktree를 사용하고 새 worktree는 만들지 않았다. C 여유 60 GiB, D 여유 794 GiB였으며 Unity `Library`를 공유하지 않았다. 모든 Unity lane은 현재 worktree의 `./run_tests.sh`만 순차 실행했다. 각 호출은 고유한 `/mnt/d/J2M/evidence/enemy-glide-extraction/<run>` 및 D build root를 사용했고, wrapper가 `CODEX_VALIDATION_ROOT`, `TEST_RESULTS_ROOT`, `TEST_LOG_ROOT`, `CAPTURE_ROOT`, `PLAYER_BUILD_ROOT`, graphics 인자를 설정했다. 각 manifest에는 명령·UTC 시작/종료·exit code, source/test/helper/runner/input hash, 선택 fullname/result, XML/log/capture 경로와 SHA-256이 있다. `--print-config`와 `--dry-run`은 현재 C legacy projectPath와 해당 D 출력 경로를 확인했다. graphics dry-run은 `-terminalIrisQualityOutput`, `-terminalIrisEvidenceBundleId`, `-terminalIrisEvidenceRunId`를 Windows Unity 호출에 실제 전달했다.

## G01–G11 추적

아래 표의 모든 variant는 [66-variant 입력·관측·기대·assertion·record 추적표](/mnt/d/J2M/evidence/enemy-glide-extraction/variant-traceability.json)에 개별 행으로 펼쳤다. 그 파일에는 각 호출의 실제 `before` state/entity/pending/kinematic, Tick·seam·record ID, baseline 기대 `after`/proposed state, intent/write/update/event/presentation/hash/trace, NUnit fullname 및 assertion fixture 위치가 있다. 공통 fixture는 새 executor API를 참조하지 않고 baseline에서도 실행됐다. 실제 assembly는 G01–G11 `Game.Integration.Simulation.Tests`, 기존 focused Core/Gameplay.Tests, replay `Game.Integration.Replay.Tests`, 구조 검사 `Game.TestInfrastructure`로 XML에서 확인했다.

| Case | 필수 variant와 대표 경계 | record | 실제 assertion/관측 |
| --- | --- | ---: | --- |
| G01 | delay 0/1/2, Patrol delay, nonzero phase, zero recovery, zero windup/cooldown | 28 | tick별 exclusive deadline·sequence·same-Tick 재시작 방지·첫 억제 해제; 기존 `GlideOverSolidTests` 병행 |
| G02 | Windup/Recovery/Active/Cooldown, 각 release, state 없음, module 없음 | 10 | 반복 query, intent/자율 facing 억제와 첫 해제, no-write record |
| G03 | 일반 Solid 차단 시작/해제, Active IgnoreSolid 재탐지 성공/실패, axis 세 가지와 최대거리 축 수평/수직, 다른 face, desired distance 이내 | 11 | fresh target/locked 보존, chase 전략 실패 시 face·거리·축 fallback. G03 mutation도 실패 검출 |
| G04 | unsettled voluntary/다른 mode/settled, Active fresh intent, 목표 소실 patrol fallback | 6 | locked step과 다른 실제 intent, 두 Tick의 좌표 `(1,0)`→`(2,0)`·Right facing·Active 유지 |
| G05 | Solid 만료/반복, 비Solid 이동 Tick/다음 Tick, 같은 planar의 다른 face | 6 | WantsRecover 유지, 이동 Tick recovery 소급 금지, 다음 pre-movement 회복; `GliderAirborneP0CoreTests` 병행 |
| G06 | Windup/Active/Recovery 비참여와 복귀, 만료 전 Active, state 없는 비참여 | 9 | 비참여 중 저장 deadline 불변, 복귀 Tick 9 기준 ActiveUntil 12·RecoveryUntil 11·CooldownUntil 10 |
| G07 | LookupNone/HpZero/Marked/Dead/Detached × state 유무, Missing-absent | 11 | lookup 거부는 기록 유지, 도달한 invalid source는 기존 기록만 clear, no-state no-op |
| G08 | Patrol/Chase × Windup/Recovery, 각 suppressed/boundary/release Tick | 12 | 적용 전 snapshot의 pending reaction 보류·소비, cooldown/facing/event capture |
| G09 | nonlethal, lethal, default death, arbitrary voluntary continuation | 5 | 비치명 RecoveryUntil·interrupt metadata/event 순서, 치명 정리, 비대상 무 interrupt; 네 실제 fullname Passed |
| G10 | 동일 runtime 두 source의 forward/reverse 입력 순서 | 2 | 별도 sequence/delay/state와 canonical determinism hash·ordered event |
| G11 | 실제 `EnemyAi_GlideChaser.asset` | 4 | asset→compile→provider→pipeline Ready tick 1, Windup 240, Active 255, Recovery 435 및 presentation 신호 |

G07의 `Missing-present`는 `WorldState.RemoveEntity`가 같은 연산에서 Glide 기록을 지우므로 실제 저장 상태로 만들 수 없어 제외했다. HP0의 state-present 입력은 damage가 일반적으로 state를 지운 뒤 record를 다시 주입한 guard characterization이며 일반 damage 흐름으로 주장하지 않는다. authoring이 허용하지 않는 조합을 억지로 생성하지 않았다.

capture schema는 명시적 Glide 상태의 public semantic 필드, source/target pose·cooldown, pending, kinematic, ordered write/update/event, intent, Glide/kinematic presentation, hash/trace를 포함한다. 직접 Logic 호출의 proposed write는 적용 전 snapshot과 구분된다. 직접 호출에는 `TickResult`가 없어 hash/trace/event/presentation을 관측 불가로 표시하고, pipeline의 batch proposal은 trace에 있으므로 단일 state 값으로 꾸미지 않았다. no-op도 빈 output 배열을 가진 record다. [비교기](/mnt/d/J2M/evidence/enemy-glide-extraction/glide_capture_check.py)는 case/variant/record/schema/선택 fullname의 완전성과 순서를 먼저 검사했다. 필드/상태 필드 누락 및 필드/record 순서 변경 다섯 probe(공통 input key 누락 포함)를 모두 거부했다. [동결 계약](/mnt/d/J2M/evidence/enemy-glide-extraction/capture_contract.json)의 66 variant·104 ordered record에 대해 baseline/candidate 값·순서 차이는 **0**이었다. 동일 구현의 반복 replay 결과만으로 전후 동등성을 주장하지 않는다.

[최종 자동 gate](/mnt/d/J2M/evidence/enemy-glide-extraction/review-final-gate.json)는 양측의 모든 필수 XML 선택/Passed/skip 0, 104 record 비교, 최종 source identity, 재검증 Windows exporter 사본 114개 및 각 결과 파일의 현재 SHA-256을 재검사해 PASS였다.

## 실제 lane 결과

모든 항목은 `./run_tests.sh`를 고유 D 출력 환경에서 호출했다. `full --filter`는 broad full 실행을 뜻하지 않으며, EditMode만 선택되는 항목의 PlayMode 0개는 별도 미선택이다. 표의 수는 XML의 개별 `test-case` 기준이고 skip/실패는 모두 0개다.

| 명령/선택 | baseline | candidate |
| --- | --- | --- |
| `full --filter 'GlideOverSolidTests,GliderAirborneP0CoreTests,EnemyGlideStateMigrationCoreTests,PlayerFlipActiveGlideLandingCoreTests,EnemyKinematicContinuationBlockedReactionCoreTests'` | [107/107](/mnt/d/J2M/evidence/enemy-glide-extraction/review-baseline-focused-20260925T154137Z-764f0dc6/manifest.json) | [107/107](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-focused-20260925T160128Z-beb56af6/manifest.json) |
| 공통 fixture + `EnemyAiScenarioTests.GlideActive_,EnemyAiScenarioTests.GlideCooldown_` | [20/20](/mnt/d/J2M/evidence/enemy-glide-extraction/review-baseline-expanded-20260925T154341Z-31016069/manifest.json) | [20/20](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-expanded-20260925T160336Z-a493c33b/manifest.json) |
| `full --filter 'EnemyAiScenarioTests.GlideActive_,EnemyAiScenarioTests.GlideCooldown_'` | [7/7](/mnt/d/J2M/evidence/enemy-glide-extraction/review-baseline-scenario-20260925T154552Z-d0f06e4d/manifest.json) | [7/7](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-scenario-20260925T160604Z-3f4f19a0/manifest.json) |
| `full --filter 'EnemyAiProfileAssetContractTests.MigratedGlide_ProfileHasBehaviorOwnerAndNoLegacyMovementSkillCapability,WorldSnapshotAndPresentationTests.GlidePresentation_'` | [5/5](/mnt/d/J2M/evidence/enemy-glide-extraction/review-baseline-asset-20260925T154830Z-1b4c3e4c/manifest.json) | [5/5](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-asset-20260925T160845Z-d9e04cf5/manifest.json) |
| `--integration-replay --filter GliderAirborneP1ReplayTests` | [3/3](/mnt/d/J2M/evidence/enemy-glide-extraction/review-baseline-replay-20260925T155118Z-8674fe62/manifest.json) | [3/3](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-replay-20260925T161108Z-2b4cf799/manifest.json) |
| `UNITY_GRAPHICS=1 ./run_tests.sh core` | [406/406](/mnt/d/J2M/evidence/enemy-glide-extraction/review-baseline-graphics-core-20260925T155247Z-93d3b41e/manifest.json) | [406/406](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-graphics-core-20260925T161258Z-dd72afbe/manifest.json) |
| candidate 전용 `EnemyGlideExecutorContractTests,EnemyGlideExecutorStructureTests,GameplayCameraShakeMixerFoundationArchitectureTests.EnemyAiContracts_DoNotOwnCameraFeedbackAuthoring` | 새 API 없음 | [6/6](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-contract-20260925T155908Z-b7b5d855/manifest.json): Integration 3, Infrastructure 2, 기존 guard 1 |

graphics core의 `TerminalProductionOffcenterFocus_ActualVictoryUsesOutputCameraAndPixelAperture`는 양측 PlayMode XML에서 Passed였다. 양측 각각 off-center 12개 고유 이미지와 D 출력 요약을 남겼다. Windows Unity exporter가 현재 worktree `TestLogs`에 새로 만든 파일 중 재검증 baseline 57개와 candidate 57개만 D로 복사했다. [baseline](/mnt/d/J2M/evidence/enemy-glide-extraction/review-baseline-windows-exporter.json)과 [candidate](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-windows-exporter.json) manifest에 각 source/copy SHA-256을 기록했다. 기존 `TestLogs` 파일은 덮어쓰거나 정리하지 않았다.

## 실패 처리와 잔여 범위

P1의 임시 mutation은 Active 진입 재탐지의 `IgnoreSolid`를 `BlockSolid`로 바꿨다. [mutation 실행](/mnt/d/J2M/evidence/enemy-glide-extraction/baseline-mutation-detection-20260925T144939Z-a71df2f5/manifest.json)은 G03에서 예상 target 10 대신 유지된 99를 관측해 Failed였고, exact backup 복원 SHA `08bd4b5b…` 확인 후 [같은 테스트 재실행](/mnt/d/J2M/evidence/enemy-glide-extraction/baseline-mutation-restored-20260925T145136Z-2208a069/manifest.json)은 Passed였다. 정상 추출에 인위적인 compile-red 단계는 만들지 않았다.

초기 baseline fixture 준비에서는 [`MarkDestroy` overload 모호성](/mnt/d/J2M/evidence/enemy-glide-extraction/baseline-characterization-v1-20260925T142019Z-f81a4780/manifest.json), [G11의 실제 initial delay 이전 관측](/mnt/d/J2M/evidence/enemy-glide-extraction/baseline-characterization-v2-20260925T142129Z-e3b672fc/manifest.json), [`SetEnemyLocomotionCooldown` overload 모호성](/mnt/d/J2M/evidence/enemy-glide-extraction/baseline-characterization-v3-20260925T142412Z-e89cd152/manifest.json)을 차례로 수정했다. [v4](/mnt/d/J2M/evidence/enemy-glide-extraction/baseline-characterization-v4-20260925T142558Z-edc97266/manifest.json)에서 original production 공통 fixture가 green이었고, 재검토로 fixture를 보강한 뒤에는 위 표의 새 baseline 전체를 다시 실행했다.

초기 candidate 전용 실행은 새 파일을 아직 포함하지 않은 Unity 생성 `.csproj` 때문에 dotnet 준비 단계에서 실패했다([v1](/mnt/d/J2M/evidence/enemy-glide-extraction/candidate-contract-v1-20260925T145844Z-952880bd/manifest.json)). 무시 대상 생성 프로젝트의 compile 목록을 현재 파일에 맞췄다. 다음 실행은 구조 테스트의 NUnit `Does.Not.Contain(Type)` 컴파일 오류였고([v2](/mnt/d/J2M/evidence/enemy-glide-extraction/candidate-contract-v2-20260925T150020Z-5539c29d/manifest.json)), 해당 assertion을 수정한 [v3](/mnt/d/J2M/evidence/enemy-glide-extraction/candidate-contract-v3-20260925T150122Z-b4a8a847/manifest.json)은 6/6 Passed였다. 두 실패는 gameplay 회귀가 아닌 준비/테스트 코드 문제로 분류했다. 재검토의 [candidate 전용 재실행](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-contract-20260925T155908Z-b7b5d855/manifest.json)도 6/6 Passed였고, 최종 공통·전용·core 결과는 모두 같은 production/test/runner 상태다. 일부 `full --filter` runner가 생성 `InitTestScene` 두 파일을 발견해 청소했다는 로그를 남겼지만 exit 0·XML Passed·최종 source identity에는 영향이 없었다.

재검증의 baseline expanded EditMode, baseline graphics core PlayMode, candidate expanded EditMode Windows Unity 로그는 runner 종료 뒤 shutdown tail을 덧붙였다. 각 run의 `manifest-initial.json`을 그대로 보존하고 현재 manifest의 `postrunOutputReconciliations`에 최초/최종 해시·mtime을 기록했다. XML과 선택 결과는 바뀌지 않았고 최종 gate는 안정된 파일 해시를 재검사했다.

세 서브 에이전트의 읽기 전용 재검토에서 production 회귀는 발견되지 않았다. 재검토가 지적한 G03 최대거리 축 우선순위의 비동거리 두 분기와 source/controlled entity pose 차이는 각각 공통 characterization과 candidate 직접 계약에 추가했다. 공통 input key가 빠진 manifest를 거부하도록 비교기를 고쳤고 probe로 확인했다. 공통 fixture가 바뀌었으므로 원래 production을 복원해 baseline을 다시 확보했고, candidate의 최종 104 record와 직접 비교했다. [baseline source/patch bundle](/mnt/d/J2M/evidence/enemy-glide-extraction/review-baseline-source-bundle)과 [candidate source/patch bundle](/mnt/d/J2M/evidence/enemy-glide-extraction/review-candidate-source-bundle)은 staged/unstaged patch 본문과 신규 파일 내용을 해시와 함께 보관한다. 강화한 최종 gate는 저장된 identity 보고서만 읽지 않고 현재 production/test 및 일곱 candidate run의 입력 해시를 재계산한다.

C01–C14의 소유권·관측 시점·Solid/face·timing·semantic 비교·lookup/topology·전략 호출·억제/attack 계약은 위 G matrix와 원래 인접 fixture로 보존했다. 계획 R01–R11 중 미사용 helper, ID/semantic 비교, replay 별도 lane, fallback/pending 보강, 공격 네 경로, 실제 asset 경로, graphics gate, null 한정, 복귀 Tick deadline, 전체 production freeze 지적을 각각 코드 경계와 증거로 닫았다. `CurrentPolicy`는 이번 추출에서 동작 보존 대상으로만 취급하며 영구 밸런스 규칙으로 승격하지 않는다.

broad unfiltered full, UI, Player build, 성능, 수동 시각 검증은 계획된 범위여서 실행하지 않았다. 기존 full lane의 무관한 red baseline을 이번 선별 결과로 해결했다고 주장하지 않는다. 남은 위험은 이 선별 집합 밖의 조합과 수동 시각 품질이며, Jump/Charge 후속 착수는 별도 작업이다.
