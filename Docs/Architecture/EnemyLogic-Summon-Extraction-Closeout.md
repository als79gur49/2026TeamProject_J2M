# EnemyLogic Summon 실행 책임 추출 결과

2026-09-25 실행 기록. 기준 계획은 [EnemyLogic 행동별 실행 책임 추출 계획](./EnemyLogic-Behavior-Extraction-Implementation-Plan.md)이다. 이 결과는 **Summon 하나**에 한정한다. 구현과 선택한 Summon 관측 범위의 전후 동등성은 확인했지만, core PlayMode의 그래픽 테스트 4개가 기본 lane에서 skip되고 보충 그래픽 실행의 1개가 실패했으므로 계획 §10의 엄격한 전체 검증 완료로 판정하지 않는다.

## 구현과 경계

- `Gameplay_EnemyAI/Runtime/EnemySummonExecutor.cs`에 `internal static` 실행기를 추가하고 `Commit`, `Cancel`, `ShouldSuppressActive`, `ShouldSuppressImminent` 및 Summon 전용 helper/상수를 옮겼다. `EnemyLogic`의 source lookup 실패, topology 비참여, 정상 commit, active/imminent 억제 호출은 기존 순서와 guard에서 실행기로 위임한다.
- 실행기는 entity ID와 불변 runtime을 명시적으로 받고 지속 가변 상태·coordinator 역참조·직접 spawn/ID 할당·View/audio/VFX 호출을 갖지 않는다. `WorldState`의 상태 소유권, snapshot 조회 시점, pre-movement commit/batch와 후속 resolver/materializer 경로는 그대로다. Profile/compiler/runtime type, schema, authoring asset, Scene/Prefab/ScriptableObject의 production 변경은 없다.
- 새 source/test helper/contract test에는 각각 `.meta`가 있다. `GameplayCameraShakeMixerFoundationArchitectureTests`의 파일 기반 guard는 새 실행기까지 읽는다. 구조 검토는 네 위임 경로와 C01–C14, OR short-circuit, null runtime query, pending blocked reaction 소비 경계를 확인했다. 검토만으로 runtime 통과를 주장하지 않는다.

## 기준과 capture 계약

기준 HEAD는 `2f07eb44078d3d66ef0c5eb1be9781ff186d59d7`로 계획 SHA와 같았다. 기존 branch의 ahead commit과 사용자 문서 변경은 유지했다. 기존 C worktree를 그대로 사용했으며 `j2m-worktree-audit`는 통과했다. 새 evidence는 `/mnt/d/J2M/evidence/enemy-summon-extraction`, build root는 `/mnt/d/J2M/builds/enemy-summon-extraction`이다. runner에는 매 호출마다 새 root와 `CODEX_VALIDATION_ROOT`, `TEST_RESULTS_ROOT`, `TEST_LOG_ROOT`, `CAPTURE_ROOT`, `PLAYER_BUILD_ROOT`를 명시했다. [`./run_tests.sh --print-config`](/mnt/d/J2M/evidence/enemy-summon-extraction/baseline-config-20260925T075132Z-9eb6f2cd) 및 실제 XML/log 경로로 적용을 확인했다. Windows Unity가 WSL capture 환경변수를 상속한다는 가정 없이 NUnit XML 출력에서 Summon capture를 D 경로로 추출했다. 보충 그래픽 테스트의 Windows exporter가 실제로 C worktree `TestLogs`에 쓴 73개 파일은 확인 후 [D evidence로 이동](/mnt/d/J2M/evidence/enemy-summon-extraction/candidate-graphics-core-skip-closure-20260925T090626Z-2572483f/captures/windows-exported)했다.

최종 [baseline freeze](/mnt/d/J2M/evidence/enemy-summon-extraction/baseline-freeze-v3.json)와 [검증 요약](/mnt/d/J2M/evidence/enemy-summon-extraction/validation-summary-v3.json)은 명령·시각·exit code·HEAD·staged/unstaged patch·untracked 내용과 hash·입력/설정 identity·XML/log/capture 경로를 연결한다. 이전 `baseline-freeze.json`과 `baseline-freeze-v2.json`은 과거 버전 기록이며 최종 비교 기준이 아니다. 최종 원래 production `EnemyLogic.cs` SHA-256은 `c8bbcde3919d1a20…`, candidate는 `08bd4b5b91ac91b2…`; 양측 공통 scenario test는 `f40f900d83035e16…`, capture helper는 `fbc032f810ba019a…`로 같다. 원래 production으로 재실행할 때 candidate 전용 실행기와 직접 계약 테스트는 제외했고, 복원 시 저장한 candidate 파일의 hash를 확인했다. 양측 공통 test/helper/input/config identity 일치 여부도 비교기가 검사한다. 각 run의 `postcapture-manifest-v3.json`에는 선택된 전체 test fullname·결과, XML, capture 경로와 hash를 보충 기록했다.

[case 계약](/mnt/d/J2M/evidence/enemy-summon-extraction/capture_contract.json)은 각 세부 case의 입력·기대 경계·test method·실제 assertion 위치·record ID·기대 Tick/호출을 고정한다. [49-case 추적표](/mnt/d/J2M/evidence/enemy-summon-extraction/case-traceability-v3.json)는 각 case와 97개 record의 양측 capture 파일·JSON pointer를 따로 연결한다. [비교기](/mnt/d/J2M/evidence/enemy-summon-extraction/capture_check.py)는 공통 95개 test fullname·결과·순서, case/record 누락·중복·추가, 상태·pending reaction·entity·요청·표현의 하위 직렬화 필드, case별 source 상태·존재 조건, 직접 seam의 전후 snapshot·제안 write, 기대 Tick을 먼저 검사한다. 하위 필드·source 존재를 일부러 제거한 세 입력을 [검사기 변이 probe](/mnt/d/J2M/evidence/enemy-summon-extraction/checker-mutation-probe-v3.json)가 모두 거부했다. 그 뒤 이벤트·요청·trace를 정렬하거나 gameplay 값을 정규화하지 않고 순서와 값을 비교한다. 실행 시각·출력 경로 같은 runner metadata만 capture 외부에 둔다. 정상 no-op은 명시적 record와 빈 write/trigger 목록으로 남겼다.

| 사례 | 실제 assertion/test와 관측 경계 |
| --- | --- |
| S01 | `BehaviorSummon_InitialDelayCooldownWindupRecoveryParity`: 7 Tick의 초기 지연, Windup, commit, Recover 종료, cooldown/다음 시작 |
| S02a–h | `BehaviorSummon_Characterization_SuppressionOptionsIncludeFirstReleaseTick`: Windup/Recover 억제 옵션 4조합 × recovery 0/2, 각 5 Tick의 이동·facing·해제. 억제·비억제·첫 해제 Tick 모두 `Direction.Right`를 명시적으로 assert |
| S03 | `BehaviorSummon_Characterization_MovingMaxAliveSourceStartsAfterChildRemoval`: 이동 source의 max-alive 도달과 자식 제거 후 시작. 두 Tick의 이동 의도와 `Direction.Right`를 assert |
| S04a–c | 기존 topology Windup 사례와 새 Recover 중단/복귀, 미초기화 off-topology no-op; 상태·cooldown·signal 경계 |
| S05 | `BehaviorSummon_Characterization_InvalidSourceCancelDistinguishesMissingAndInactiveState`: 5가지 invalid source × 상태 4종의 취소 write/log/no-op 및 실제 엔티티 부재 1건. `Lookup`은 `aiMode=None`으로 participation lookup을 거부하되 기존 상태는 남긴다. 실제 `RemoveEntity`는 Summon 상태도 제거하므로 엔티티 부재+상태 잔존 조합은 `WorldState.RemoveEntity`에서 도달 불가능하다. 직접 context의 전후 WorldState 상태와 제안 write를 별도 기록한다. |
| S06 | `BehaviorSummon_Characterization_DifferentSourceStatesKeepCanonicalOrderAcrossInputPermutation`: source 입력 순서 2가지, child ID/metadata/event 순서/hash |
| S07a–c 및 production | execution Tick 이동 후 source pose 배치, 막힌 배치 no-op, 공격 후 source 무효 skip, production profile의 execution Tick 위치와 다음 Tick 이동 |
| S08a–b | 공개 Logic query 두 번의 무부작용, trigger sink 부재 시 Recover write/update 지속; 후자는 직접 context의 제안 상태도 기록 |
| S09a–c | Patrol/Chase × imminent/active/release에서 pending blocked reaction 유지·소비, cooldown, facing/chase 결과. 임시 imminent 억제 변이에서 관련 assertion 2개가 실패했고 원복 후 6개가 통과했다. |
| S10 | Summon runtime 없는 profile의 통상 patrol, 상태 미생성·write/trigger 없음 |

직접 Logic/context 사례에는 TickResult와 materialized request·presentation·hash/trace가 존재하지 않으므로 `<not-observable>`로 명시한다. pipeline 사례의 상태·entity·요청/이벤트/표현·결정성 hash·ordered trace는 실제 TickResult에서 기록한다. 입력 entity capture에는 `markedForDeath`가 포함된다. S05의 도달 불가능 조합 외에 필수 세부 case를 생략하지 않았다.

## 실행 결과와 남은 gate

| 대상 | 명령 | 실제 결과·증거 |
| --- | --- | --- |
| baseline 공통 | `./run_tests.sh full --filter 'BehaviorSummon,MigratedSummonRuntimeContractTests,EnemyProfileContractReplayTests,KaliSummonedUnitRuntimeContractTests'` | EditMode 95 passed, 0 failed/skipped; PlayMode 0 selected는 해당 fixture가 EditMode여서 예상됨. [XML·capture·manifest](/mnt/d/J2M/evidence/enemy-summon-extraction/baseline-common-targeted-v3-20260925T110015Z-932b3d9c) |
| candidate 공통 | 같은 명령·공통 input/helper/config | EditMode 95 passed. [XML·capture·manifest](/mnt/d/J2M/evidence/enemy-summon-extraction/candidate-common-targeted-v3-20260925T110549Z-076d5e71) |
| capture 비교 | `python3 capture_check.py compare BASELINE_ROOT CANDIDATE_ROOT` | 양측 49 case·97 record 및 전체 선택 테스트 완전성 통과, 값/순서 차이 0. [comparison.json](/mnt/d/J2M/evidence/enemy-summon-extraction/candidate-common-targeted-v3-20260925T110549Z-076d5e71/captures/comparison.json) |
| candidate 전용 | `./run_tests.sh full --filter 'EnemySummonExecutorContractTests,GameplayCameraShakeMixerFoundationArchitectureTests'` | EditMode 21 passed(직접 계약 4, 구조 guard 17), 0 failed/skipped. [XML·manifest](/mnt/d/J2M/evidence/enemy-summon-extraction/candidate-contract-guard-v3-20260925T110820Z-18c1cdc8) |
| baseline / candidate core | 각 source identity에서 `./run_tests.sh core` | 양측 EditMode 293 passed; PlayMode 113 selected 중 109 passed, 4 skipped(그래픽 장치/전용 lane 필요). runner exit 0. [baseline](/mnt/d/J2M/evidence/enemy-summon-extraction/baseline-core-v3-20260925T110312Z-7170f2a4), [candidate](/mnt/d/J2M/evidence/enemy-summon-extraction/candidate-core-v3-20260925T111014Z-069ad435) |

초기 targeted에서 production replay assertion 하나가 다음 Tick의 child 위치를 execution Tick 배치 위치로 잘못 기대했다. 원래 production에서 execution Tick assertion으로 수정하고 green을 확인한 뒤 추출했다. 이후 capture helper·case 계약 보강 시 원래 production을 다시 적용해 baseline을 재확보했다. 재검토에서 추가로 지적된 S02/S03 방향 assertion, 하위 field schema, case별 추적표를 보강한 뒤 원래 production에서 v3 baseline을 다시 확보했다. 이전 실행과 실패 artifact는 덮어쓰지 않았다. 읽기 전용 runtime 재검토에서 production diff의 구체적 새 결함은 발견되지 않았다.

엄격한 core 전체 선택 목록 gate는 **미완료**다. headless에서 skip된 그래픽 테스트 4개를 같은 candidate의 `UNITY_GRAPHICS=1 ./run_tests.sh core --filter ...`로 실행해 3개가 통과했지만 `TerminalProductionOffcenterFocus_ActualVictoryUsesOutputCameraAndPixelAperture`는 전용 인자 부재로 실패했다. 전용 `./run_tests.sh terminal-production-offcenter-focus`에서는 evidence ID를 설정한 뒤 실제로 실행됐으나 `StageLaunchContextStore.TryPeek` assertion이 실패했다. 원래 production 소스로 같은 전용 lane을 재실행해 **같은 테스트·같은 assertion(1805행)에서 실패**함을 확인했고 candidate를 저장 hash로 복원했다. 이 실패는 extraction 전에도 재현되는 그래픽/scene bootstrap 기준 실패이며 Summon candidate 차이로 보지 않는다. [그래픽 필터](/mnt/d/J2M/evidence/enemy-summon-extraction/candidate-graphics-core-skip-closure-20260925T090626Z-2572483f), [전용 lane baseline](/mnt/d/J2M/evidence/enemy-summon-extraction/baseline-offcenter-dedicated-v2-20260925T091513Z-c2e221f2), [전용 lane candidate](/mnt/d/J2M/evidence/enemy-summon-extraction/candidate-offcenter-dedicated-v2-20260925T091026Z-a5e2096b). 0개 선택·skip·환경/설정 실패를 통과로 계산하지 않았다.

Broad unfiltered full, UI lane, Player build, 성능, 수동 시각 검증은 실행하지 않았다. 변경 범위가 Summon runtime 위임과 test/guard에 한정되고 UI·표현 asset/schema를 바꾸지 않았기 때문이다. 이 미실행 범위와 그래픽 gate 때문에 project-wide/full regression 통과를 주장하지 않는다. 다른 행동의 추출은 후속 후보일 뿐 이 작업에서 시작하지 않았다.
