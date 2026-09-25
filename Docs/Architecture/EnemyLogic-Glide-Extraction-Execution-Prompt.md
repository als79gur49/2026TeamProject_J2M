# EnemyLogic Glide 추출 실행 프롬프트

아래 본문을 실행 세션에 전달한다. 이 파일 작성 자체는 runtime 구현 시작이나 검증 완료를 의미하지 않는다.

---

현재 J2M Unity/C# 저장소에서 `Docs/Architecture/EnemyLogic-Glide-Extraction-Implementation-Plan.md`를 기준으로 Glide 실행 책임 추출을 P0부터 P4까지 수행하라. 계획 설명만으로 종료하지 말고 baseline 확보, 구현, 변경 전후 검증, 결과 문서 작성까지 완료하라. 단계별 gate를 충족하면 별도 재승인 없이 다음 단계로 진행하라. 범위 밖 정책 결정이나 사용자 변경과의 해결 불가능한 충돌은 근거와 필요한 결정을 보고하라.

## 1. 시작과 범위

먼저 `git status --short --branch`, `git diff --stat`를 실행하고 staged/unstaged/untracked 내용을 확인하라. 기존 사용자 변경과 ahead commit을 보존하라. 이전 세션에서 만든 계획과 README 변경을 새 변경으로 오인해 덮어쓰거나 되돌리지 마라.

다음을 읽고 적용하라.

- `AGENTS.md`
- `Docs/Architecture/README.md`
- `Docs/Architecture/Tick-Simulation-Canonical-Spec.md`
- `Docs/Testing/Gameplay-Test-Automation-Guide.md`
- `AI_GIT_COMMIT_RULES.md`
- `Docs/Architecture/EnemyLogic-Glide-Extraction-Implementation-Plan.md` 전체, 특히 C01–C14, G01–G11, R01–R11
- `Docs/Architecture/EnemyLogic-Behavior-Extraction-Implementation-Plan.md`
- `Docs/Architecture/EnemyLogic-Summon-Extraction-Closeout.md`
- 사용 가능한 `gameplay-contract-hardening` skill

상세 계약과 사례의 기준은 Glide 계획이다. 이 프롬프트의 요약을 근거로 필수 variant나 검증 조건을 축소하지 마라. 계획의 문서 작성 당시 상태와 과거 검토 결과는 현재 구현/검증 증거가 아니다.

목표는 `internal static EnemyGlideExecutor`에 Glide pre-movement lifecycle, 시작/Active 진입 목표 선택, Windup/Recovery 억제 질의를 응집하는 것이다. production 변경은 `EnemyLogic.cs`, 신규 `EnemyGlideExecutor.cs`와 `.meta`로 제한한다. 필요한 공통 characterization, capture helper, candidate 계약/구조 검사, 결과 문서는 함께 작성한다.

일반 이동/fallback/이동 시간, 일반 Active 탐지 옵션, 공격 단계 Recovery, WorldState/schema/compiler/profile/asset은 기존 소유자에 둔다. Jump/Charge 추출, dead-code 정리, 범용 executor/FSM 도입, 최적화, 밸런스 변경을 섞지 마라. commit/push/PR/merge는 이 실행 범위에 포함하지 않는다.

## 2. P0 — 기준과 실행 환경 확정

1. 계획 기준 SHA `2692bd37f35f3facb0c4bdbc240078f2a968fea0`와 현재 상태를 비교하라. 이후 production 변경이 있으면 보존한 현재 tree를 새 baseline으로 명시하고 계획/manifest의 기준을 갱신하라. 현재 파일을 과거 SHA로 일괄 되돌리지 마라.
2. 전체 production identity를 동결하고 허용 production 차이와 공통/candidate 전용 test 차이를 구분하라. staged/unstaged patch, 신규 파일 내용/hash, runner/config/input identity까지 수집하라.
3. 계획 §2 helper inventory와 모든 caller를 심볼로 재확인하라. G01–G11을 구체 variant로 펼쳐 입력, 관측 Tick/호출, 예상 상태/출력, 실제 test/assertion, record ID를 연결하라. 도달 불가/authoring 불허 조합은 소스 근거로 제외하라.
4. 각 test의 실제 assembly와 runner 선택을 확인하라. 신규 runtime/pipeline 테스트는 Integration, 구조-only 검사는 Infrastructure에 둬라. 기존 Core fixture 이름을 새 pipeline 테스트 배치 근거로 쓰지 마라.
5. 모든 Unity lane은 현재 검증 worktree의 `./run_tests.sh`로 순차 실행하라. Unity를 직접 호출하거나 다른 worktree의 projectPath를 사용하지 마라.
6. storage 확인은 `j2m-worktree-audit`로 수행하라. 기존 C legacy worktree는 유지하라. 새 worktree가 필요하면 D 여유 30 GiB 이상을 확인하고 `j2m-worktree-add`로 `/mnt/d/J2M/worktrees`에 생성한 뒤 resolved project path와 private Library를 확인하라. C 여유 10 GiB 미만이면 알려라.
7. 매 호출마다 새 `/mnt/d/J2M/evidence/enemy-glide-extraction/<unique-run>`을 만들고 `CODEX_VALIDATION_ROOT`, `TEST_RESULTS_ROOT`, `TEST_LOG_ROOT`, `CAPTURE_ROOT`를 명시하라. `PLAYER_BUILD_ROOT`는 `/mnt/d/J2M/builds/enemy-glide-extraction/<unique-run>`으로 설정하라. `--print-config`와 `--dry-run`으로 실제 project/output 경로를 확인하라.
8. manifest 수집기를 준비하라. 명령, 시작/종료 시각, exit code, source/test/helper/runner/input/config hash, 개별 선택 fullname/result, XML/log/capture 경로와 hash를 기록하라. 준비·runner 실패 후 후속 단계가 자동 실행되지 않도록 실패를 전파하라.

## 3. P1 — 원래 동작과 비교 기준 확보

추출 전 production에서 공통 테스트를 실행하라. 기존 assertion을 재사용하고 coverage가 부족한 경우만 characterization을 보강하라. 특히 다음을 확인하라.

- initial delay/zero timing/같은 Tick 재시작 방지 및 첫 억제 해제
- 시작 일반 탐지와 Active 진입 IgnoreSolid 재탐지, 실패 시 locked 값 보존
- 실제 fallback intent/좌표/facing 및 locked step과 다른 이동
- Solid 만료 유지, 비Solid 이동 다음 Tick 회복, face-aware 판정
- topology 비참여 중 기록 유지와 복귀 Tick 기준 새 phase deadline
- source 무효화/부재의 도달 가능한 경로, state 없음/있음의 차이
- Glide 억제와 pending blocked reaction의 적용 전 snapshot 판단
- 공격 중단의 비치명/치명/적용불가, 타이머·metadata·event 순서
- 동일 runtime을 공유하는 복수 source 격리와 입력 순서 변형
- 실제 GlideChaser asset→compile→provider→pipeline lifecycle 및 presentation 신호

새 실행기 API가 없는 baseline에서도 공통 테스트가 실행되어야 한다. 현재 public Logic/pipeline seam을 사용하라. production asset smoke를 합성 profile 테스트로 대체하지 마라.

계획 §6에 따라 명시적 상태/pose/cooldown/pending/kinematic/write/event/presentation/hash/trace capture를 수집하라. 직접 context에서는 제안 write와 적용 전 상태를 구분하라. no-op도 빈 output record로 기록하라. 관측 불가능한 필드는 이유를 명시하라.

case/variant/record/schema/선택 fullname 완전성을 먼저 검사하는 비교기를 준비하라. record/state field 누락과 순서 변이를 거부하는지 확인하라. 의미 있는 행동 경계 하나의 임시 mutation을 테스트가 검출하는지 확인하고 원복 hash 및 green을 기록하라. 정상 리팩터링에 인위적인 stub/compile-red 단계를 만들지 마라.

공통 harness/input/config를 수정하면 원래 production에서 baseline을 다시 확보하라. 설명되지 않은 touched failure가 있거나 필수 baseline gate가 미완료이면 P2로 진행하지 마라. 테스트 기대값을 candidate 동작에 맞춰 재생성하지 마라.

## 4. P2 — 실행 책임 추출

계획 §2의 실사용 helper를 옮기고 §3의 `Commit`, `ShouldSuppressMovement`에 위임하라. 다음을 보존하라.

- patrol init → Jump → Charge → Glide → Utility → Summon의 순서 및 원래 snapshot
- lookup/topology early return: Glide Cancel/suspend/timer extension을 새로 추가하지 않음
- lifecycle 진행 → delay 감소 → 시작 판단 → 시작 직후 lifecycle 진행, 최대 8회 loop
- `_entityId` 기반 state/write/log와 `source.entityId` 기반 pose 조회의 기존 구분
- 현재 `_tileFeatureDefinitions`를 매 호출 전달; 지속 가변 상태·cache·coordinator 역참조 없음
- runtime-null/state-absent 억제 false; snapshot-null의 새 계약 없음; query에 writer/log 없음
- OR short-circuit, 전략 호출 횟수와 시점, semantic property 비교, 기존 로그 필드·순서
- attack Recovery와 일반 이동/탐지 consumer의 기존 소유권

`GameplayCameraShakeMixerFoundationArchitectureTests` 등 파일 기반 guard가 새 실행기까지 검사하도록 보완하라. 신규 Unity source/test/helper에는 `.meta`를 함께 둬라. candidate 전용 직접 계약 검사는 의미 있는 경계를 검증하고 공통 public 경로의 integration 검사를 유지하라.

## 5. P3 — 공통·전용·core 검증

아래 명령은 P0의 D 출력 환경과 manifest wrapper 안에서 실행하라. 공통 characterization/G11은 실제 배치 assembly에 맞는 lane/filter로 추가하라. 양측 공통 목록을 동일하게 유지하라.

```bash
./run_tests.sh full --filter 'GlideOverSolidTests,GliderAirborneP0CoreTests,EnemyGlideStateMigrationCoreTests,PlayerFlipActiveGlideLandingCoreTests,EnemyKinematicContinuationBlockedReactionCoreTests'
./run_tests.sh full --filter 'EnemyAiScenarioTests.GlideActive_,EnemyAiScenarioTests.GlideCooldown_'
./run_tests.sh full --filter 'EnemyAiProfileAssetContractTests.MigratedGlide_ProfileHasBehaviorOwnerAndNoLegacyMovementSkillCapability,WorldSnapshotAndPresentationTests.GlidePresentation_'
./run_tests.sh --integration-replay --filter GliderAirborneP1ReplayTests
```

G09의 비치명/치명/default death/임의 voluntary continuation 네 fullname이 실제 선택됐는지 확인하라. Core category만으로 replay나 Extended의 선택을 추정하지 마라. `full --filter`는 해당 assembly를 선택할 수 있지만 실제 XML을 판정 근거로 삼아라.

candidate 전용 `EnemyGlideExecutorContractTests`와 실제 변경한 구조 guard를 실행하라. baseline에는 새 executor API/파일을 요구하는 테스트를 이식하지 마라.

baseline/candidate 양측에서 다음 최종 gate를 실행하라.

```bash
UNITY_GRAPHICS=1 ./run_tests.sh core
```

이 명령에는 같은 D run 하위의 `TERMINAL_IRIS_QUALITY_OUTPUT_DIR`와 유효한 `TERMINAL_IRIS_EVIDENCE_BUNDLE_ID`, `TERMINAL_IRIS_EVIDENCE_RUN_ID`를 명시하라. 현재 off-center test 요구와 dry-run 전달값을 확인하라. 기본 headless core를 실행했다면 별도로 기록하고 graphics gate를 대체했다고 주장하지 마라. Windows exporter의 실제 출력도 확인하여 새 파일만 hash와 함께 D evidence로 보관하라.

완전성 검사를 통과한 공통 capture의 값·순서 차이가 0이어야 한다. 같은 구현을 두 번 실행한 결정성 결과만으로 전후 동등성을 주장하지 마라. candidate의 최종 공통·전용·core 결과는 같은 production/test/runner 상태에 연결하라. skip/필수 test 0개/XML 부재는 통과가 아니다.

실패는 baseline 기존 문제, candidate 회귀, 환경/선택/timeout 문제로 근거에 따라 분류하라. 엄격한 graphics core 미완료와 Glide focused 결과는 분리하되 최종 gate를 임의로 완화하지 마라. 범위 내 문제는 수정과 필요한 재검증을 끝까지 수행하라. 정책/schema 변경이 필요하면 먼저 변경 의도와 범위를 다시 정하라.

## 6. P4 — 결과와 완료 판정

`Docs/Architecture/EnemyLogic-Glide-Extraction-Closeout.md`에 실제 구현 경계, baseline/candidate identity, G01–G11 추적표, 실행 명령/개별 결과/증거 경로, 변경 전후 비교, 실패와 해결 과정, 미실행 이유, 남은 위험을 기록하라. 계획의 실행 상태와 README 연결을 실제 결과에 맞춰 갱신하라.

완료 조건은 계획 §9를 따른다. G01–G11 필수 variant mapping, capture 완전성 및 차이 0, 후보 계약/구조 검사, 양측 graphics core gate, 같은 candidate의 최종 evidence가 필요하다. 자료가 부족한 gate는 미완료로 남기고 정확한 이유를 보고하라.

기본 미실행 범위는 broad unfiltered full, UI, Player build, 성능, 수동 시각 검증이다. 새로운 근거가 생기면 필요한 범위만 추가하라. 실행하지 않은 범위의 통과나 project-wide/full regression 완료를 주장하지 마라.

최종 응답에는 변경 요약, 실제 테스트 결과, 전후 비교 결과, 문서/evidence 링크, 미실행과 남은 문제를 간결하게 적어라. 실행기 추출 완료를 Jump/Charge 착수로 이어가지 마라.
