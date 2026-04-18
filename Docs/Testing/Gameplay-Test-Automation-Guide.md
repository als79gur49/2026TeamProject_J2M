> Operational guide.
> Canonical architecture and gameplay behavior references start at [Docs/Architecture/README.md](../Architecture/README.md), with [Docs/Architecture/Tick-Simulation-Canonical-Spec.md](../Architecture/Tick-Simulation-Canonical-Spec.md) and [Docs/Architecture/Gameplay-Rules-Appendix.md](../Architecture/Gameplay-Rules-Appendix.md) as the active spec/rules source.

# Gameplay Test Automation Guide / 게임플레이 테스트 자동화 가이드

> This document is intentionally bilingual. Korean guidance is added for local developer readability, and the English original is preserved to avoid meaning drift during translation.
>
> 이 문서는 의도적으로 한영 병기 형태를 유지한다. 한국어 설명은 로컬 개발자 가독성을 위한 것이고, 번역 과정에서 의미가 달라지는 일을 막기 위해 영어 원문을 함께 남긴다.

## Current Validation Baseline / 현재 검증 기준점
### 한국어
- 현재 환경에서는 `./run_tests.sh core`와 `./run_tests.sh full`이 실제로 실행 가능하다.
- Stage 9 UI hardening 검증용으로 `./run_tests.sh ui` 경로를 유지한다. 이 경로는 UI EditMode assembly만 대상으로 하는 집중 검증용이며 Stage 4–8 seam preservation evidence를 담당한다.
- 현재 기준점은 다음과 같다.
  - `./run_tests.sh core`: green, Core EditMode `13 total / 0 failed`, Core PlayMode `2 total / 0 failed`
  - `./run_tests.sh ui`: green, Unity UI EditMode `155 total / 0 failed`
  - `./run_tests.sh full`: red, Unity Full EditMode `703 total / 101 failed`
  - Unity Full PlayMode는 EditMode failure 때문에 아직 실행되지 않았다.
- 후속 PR은 per-class fail histogram 기준으로 direct touched cluster와 unrelated baseline cluster를 분리해 판정한다.
- 자세한 baseline은 [Full-EditMode-Baseline-2026-04-13.md](./Full-EditMode-Baseline-2026-04-13.md)를 따른다.
- UI freeze evidence는 [UI-EditMode-Baseline-2026-04-15.md](./UI-EditMode-Baseline-2026-04-15.md)를 따른다. 이 문서는 test count ledger가 아니라 structural delta, guard evolution, runner warning status, PlayMode escalation status를 함께 기록해야 한다.
- `TutorialScene` 실씬 런타임 UI smoke가 필요할 때는 [TutorialScene-Manual-Runtime-Smoke-Plan.md](./TutorialScene-Manual-Runtime-Smoke-Plan.md)를 사용한다. 이 문서는 자동화 lane을 대체하지 않고 canonical runtime integration의 수동 companion evidence를 정의한다.
- generated stratification report는 더 이상 governance truth-source가 아니다.

### English Original
- In the current environment, both `./run_tests.sh core` and `./run_tests.sh full` are runnable.
- `./run_tests.sh ui` remains the targeted Stage 9 UI hardening path for the UI EditMode assembly only, preserving Stage 4–8 seams on one Unity-backed evidence lane.
- The current baseline is:
  - `./run_tests.sh core`: green, Core EditMode `13 total / 0 failed`, Core PlayMode `2 total / 0 failed`
  - `./run_tests.sh ui`: green, Unity UI EditMode `155 total / 0 failed`
  - `./run_tests.sh full`: red, Unity Full EditMode `703 total / 101 failed`
  - Unity Full PlayMode has not run yet because EditMode failed first.
- Follow-up PRs are judged by per-class fail histograms split into direct touched clusters and unrelated baseline clusters.
- See [Full-EditMode-Baseline-2026-04-13.md](./Full-EditMode-Baseline-2026-04-13.md) for the pinned baseline.
- Use [UI-EditMode-Baseline-2026-04-15.md](./UI-EditMode-Baseline-2026-04-15.md) for Stage 9 UI hardening evidence, including structural delta and guard-evolution interpretation.
- Use [TutorialScene-Manual-Runtime-Smoke-Plan.md](./TutorialScene-Manual-Runtime-Smoke-Plan.md) when a real-scene `TutorialScene` UI smoke pass is needed; it is the manual companion for canonical runtime-integration evidence and does not replace the automated lanes.
- The generated stratification report is no longer an active governance truth source.

## 1. Overview / 개요
### 한국어
- 이 시스템은 WSL에서 테스트를 오케스트레이션하면서 실제 빌드와 실행은 Windows `dotnet`과 Unity에서 수행하도록 고정한 게임플레이 테스트 운영 체계다.
- `Core`는 일상 개발과 pre-commit에서 사용하는 빠르고 결정적인 안전 계층이다.
- `Full`은 런타임 동작, 구조 검증, 에셋 민감 검증까지 포함하는 전체 검증 경로다.
- `Governance`는 테스트 배치 규칙을 자동으로 강제해서 사람의 기억이나 관습에 의존하지 않게 만든다.

### English Original
- This system gives the project one repeatable way to build, run, classify, and validate gameplay tests from WSL while executing Unity and `dotnet` on Windows.
- `Core` is the fast deterministic safety layer used for everyday development and pre-commit gating.
- `Full` is the broader validation path that includes runtime behavior, structure checks, and asset-sensitive coverage.
- `Governance` automatically enforces test boundaries so placement rules do not depend on memory or team habit.

## UI baseline governance / UI baseline governance
### 한국어
- UI baseline note는 단순 count bump 문서가 아니다.
- Stage 9 이후에는 다음 항목을 함께 기록해야 한다.
  - added / removed / renamed / merged / split tests
  - replaced weak guards / obsolete guards
  - responsibility shifts between layers
  - bounded UI layer migration이 완료될 때 prefab migration inventory 축소와 sunset proof
  - runner warning changes
  - PlayMode escalation status
- `Docs/Testing/UI-EditMode-Baseline-2026-04-15.md`와 이 가이드는 같은 변경에서 함께 갱신해야 한다.

### English Original
- The UI baseline note is not a count-only ledger.
- After Stage 9 it must record:
  - added / removed / renamed / merged / split tests
  - replaced weak guards / obsolete guards
  - responsibility shifts between layers
  - prefab migration inventory shrinkage and sunset proof when a bounded UI layer completes migration
  - runner warning changes
  - PlayMode escalation status
- `Docs/Testing/UI-EditMode-Baseline-2026-04-15.md` and this guide must be updated together in the same change.

## PlayMode escalation triggers / PlayMode escalation triggers
### 한국어
- UI PlayMode는 EditMode만으로 ownership behavior를 신뢰성 있게 검증할 수 없을 때만 추가한다.
- 허용 trigger:
  - real play loop가 필요한 runtime-only input routing
  - screen/popup/HUD ownership에 영향을 주는 scene lifecycle ordering / activation timing
  - runtime-only execution에 의존하는 diagnostics visibility / toggle behavior
  - EditMode 결과를 무효화할 수 있는 domain reload / play-loop behavior
- 허용되지 않는 trigger:
  - “PlayMode에서 한번 보면 좋겠다”
  - mapper / policy / reflection / presenter interaction / direct EditMode composition test

### English Original
- UI PlayMode coverage is added only when EditMode cannot credibly verify the ownership behavior being protected.
- Allowed triggers:
  - runtime-only input routing that depends on the real play loop
  - scene lifecycle ordering or activation timing that materially affects screen/popup/HUD ownership
  - diagnostics visibility or toggle behavior that depends on runtime-only execution
  - domain reload or play-loop behavior that can invalidate an EditMode-only result
- Disallowed trigger:
  - “it would be nice to see it in PlayMode”
  - mapper / policy / reflection / presenter interaction / direct EditMode composition tests

## 2. Why This System Exists / 왜 이 시스템이 존재하는가
### 한국어
- Core는 반드시 결정적으로 유지되어야 한다. 그래야 로컬 게이트 실패가 “실제 로직 회귀”를 뜻하지, 광범위한 구조 변경이나 에셋 문제를 뜻하지 않게 된다.
- 실행 기반 테스트는 Core에서 분리해야 한다. 런타임이 조합된 테스트는 느리고 범위가 넓으며, 리팩터링 중에는 불안정성이 더 커지기 때문이다.
- Governance는 수동 규율로는 유지할 수 없는 경계를 자동으로 검증하기 위해 필요하다. 잘못 배치된 테스트, 오래된 stratification 산출물, 계층 위반을 시스템이 직접 잡아야 한다.

### English Original
- Core must stay deterministic so a failed local gate means a real logic regression, not a broad architecture or asset issue.
- Execution-heavy tests are separated from Core because runtime-composed behavior is slower, wider in scope, and less stable during active refactors.
- Governance is required because manual policing does not scale; the system must detect misplaced tests, stale stratification data, and boundary violations automatically.

## 3. Test Stratification Model / 테스트 계층 모델
### 한국어
#### 실행 티어
- `Core`
  - 의미: 가장 빠르고 신호 밀도가 높은 로컬 검증 경로
  - 포함 대상: 결정적 안전성 검증, 잠금된 대표 시나리오
  - 제외 대상: 넓은 회귀 스윕, 불안정한 에셋 검증, 느린 탐색성 검증
- `Extended`
  - 의미: 개발 중 유용하지만 주 로컬 게이트는 아닌 넓은 검증 경로
  - 포함 대상: 상세 변형 케이스, 구조 검증, 비최소 시나리오 검증
  - 제외 대상: Full에 남겨야 하는 대규모 광역 검증
- `Full`
  - 의미: 전체 검증 경로
  - 포함 대상: 나머지 전체 테스트, 희귀 엣지 케이스, 대형 런타임 조합, 에셋 민감 검증
  - 제외 대상: 일상 커밋 게이트로 사용하는 행위

#### 배치 계층
- `Core`
  - 결정적 입력/출력 로직만 포함한다.
- `Infrastructure`
  - 구조, 시그니처, 생성자 형태, DI/wiring, 리플렉션 기반 검증만 포함한다.
- `Integration`
  - 런타임 실행, 다중 시스템 상호작용, 파이프라인 흐름, 조합된 게임플레이 시나리오를 포함한다.

### English Original
#### Execution tiers
- `Core`
  - Meaning: fastest high-signal path used for frequent local validation.
  - Belongs here: deterministic safety coverage and locked representative scenarios.
  - Must not include: broad regression sweeps, unstable asset checks, or slow exploratory coverage.
- `Extended`
  - Meaning: broader validation that is useful during development but not the main local gate.
  - Belongs here: variation/detail coverage, structural checks, and non-minimal scenario coverage.
  - Must not include: massive broad sweeps better reserved for Full.
- `Full`
  - Meaning: complete validation path.
  - Belongs here: all remaining tests, rare edges, large runtime combinations, and asset-sensitive coverage.
  - Must not be treated as the day-to-day commit gate.

#### Placement layers
- `Core`
  - Deterministic input/output logic only.
- `Infrastructure`
  - Structure, signatures, constructor shape, DI/wiring, and reflection-only validation.
- `Integration`
  - Runtime execution, multi-system behavior, pipeline flow, and composed gameplay scenarios.

## 4. Execution Flow / 실행 흐름
### 한국어
```text
WSL CLI
  -> run_tests.sh
    -> governance check
    -> Windows dotnet build
    -> Windows Unity.exe -executeMethod
    -> TestRunnerApi bootstrap
    -> XML result write
    -> shell XML validation + failure summary + metrics
```

- Unity는 CLI `-runTests`에 의존하지 않는다.
- Unity는 `TestRunnerCliBootstrap.RunEditMode` 또는 `TestRunnerCliBootstrap.RunPlayMode`를 통해 실행된다.
- bootstrap이 테스트 실행, XML 기록, Unity exit code를 직접 관리한다.
- shell은 이후 XML을 검증하고, 실패 테스트를 출력하고, stage metrics를 기록한다.

### English Original
```text
WSL CLI
  -> run_tests.sh
    -> governance check
    -> Windows dotnet build
    -> Windows Unity.exe -executeMethod
    -> TestRunnerApi bootstrap
    -> XML result write
    -> shell XML validation + failure summary + metrics
```

- Unity does not rely on CLI `-runTests`.
- Unity is invoked through `TestRunnerCliBootstrap.RunEditMode` or `TestRunnerCliBootstrap.RunPlayMode`.
- The bootstrap owns test execution, XML writing, and Unity exit codes.
- The shell then validates the XML, prints failed tests, and emits stage metrics.

## 5. CLI Usage / CLI 사용법
### 한국어
#### 공개 명령
```bash
./run_tests.sh core
./run_tests.sh ui
./run_tests.sh full
```

- `./run_tests.sh core`
  - 일반적인 로컬 개발 루프에서 사용한다.
  - governance 검사 후 Windows `dotnet` core build, Unity Core EditMode, Unity Core PlayMode를 순서대로 실행한다.
  - pre-commit 훅이 사용하는 명령이다.
- `./run_tests.sh ui`
  - Stage 9 이후 UI architecture hardening 및 Stage 4–8 seam preservation 검증에 사용한다.
  - governance 검사 후 Windows `dotnet` UI test build, Unity UI EditMode assembly 실행만 수행한다.
  - `TestResults/wsl-dotnet-ui.log`, `TestResults/wsl-unity-ui-editmode.log`, `TestResults/wsl-unity-ui-editmode.xml`을 남긴다.
  - `core`를 대체하지 않으며, UI slice를 넓히기 전 targeted evidence를 얻기 위한 명령이다.
- `./run_tests.sh full`
  - 안정화 직전, 통합 직전, 혹은 넓은 회귀를 조사할 때 사용한다.
  - governance 검사 후 Windows solution build, Unity Full EditMode, Unity Full PlayMode를 실행한다.
  - 첫 실패 stage에서 즉시 중단된다.

#### 종료 코드
- `0`: 성공
- `1`: 테스트 실패 또는 shell 검증 실패
- `2`: Unity 내부 bootstrap/runner/automation infrastructure 오류

#### 고급/내부 모드
- 통합 계층만 따로 실행하는 internal integration mode가 존재한다.
- 일반 개발 워크플로의 기본 경로는 아니며 `core`와 `full`을 대체해서는 안 된다.

### English Original
#### Public commands
```bash
./run_tests.sh core
./run_tests.sh ui
./run_tests.sh full
```

- `./run_tests.sh core`
  - Use for normal local development.
  - Runs governance first, then Windows `dotnet` core build, then Unity Core EditMode and Core PlayMode.
  - This is the command used by pre-commit.
- `./run_tests.sh ui`
  - Use for targeted Stage 9 UI hardening and Stage 4–8 seam-preservation validation.
  - Runs governance first, then Windows `dotnet` build for `Game.Feature.UI.Tests.csproj`, then Unity EditMode with the `ui` selection in `TestRunnerCliBootstrap`.
  - Writes `TestResults/wsl-dotnet-ui.log`, `TestResults/wsl-unity-ui-editmode.log`, and `TestResults/wsl-unity-ui-editmode.xml`.
  - It does not replace `core`; it exists to provide explicit Unity-side evidence for the UI assembly before broader UI expansion.
- `./run_tests.sh full`
  - Use before stabilization, integration, or when investigating broader regressions.
  - Runs governance first, then Windows solution build, then Unity Full EditMode and Full PlayMode.
  - Stops on the first failing stage.

#### Exit codes
- `0`: success.
- `1`: test failure or shell-side validation failure.
- `2`: runner/bootstrap/infrastructure error inside Unity or the automation layer.

#### Advanced/internal modes
- Internal integration selections exist for targeted CI/debug runs.
- They are not the normal developer workflow and should not replace `core` or `full`.

## 6. Pre-commit Behavior / Pre-commit 동작
### 한국어
- 로컬 Git pre-commit 훅은 `./run_tests.sh core`를 실행한다.
- 0이 아닌 종료 코드는 모두 커밋을 막는다.
- 의도는 엄격하지만 범위는 좁다.
  - 결정적 안전 계층의 회귀는 반드시 커밋을 막아야 한다.
  - 전체 스위트의 불안정성은 일상 로컬 커밋 게이트가 되어서는 안 된다.

### English Original
- The local Git pre-commit hook runs `./run_tests.sh core`.
- Any nonzero exit blocks the commit.
- The intent is strict but narrow:
  - deterministic safety regressions must block commits
  - full-suite instability must not become the everyday local gate

## 7. Governance System / Governance 시스템
### 한국어
- Governance가 검사하는 대상:
  - 테스트 배치 경계
  - Core purity 규칙
  - gameplay semantic query migration boundary
  - Integration 밖에 놓인 execution-based test
  - source category / override inventory 일관성
  - PlayMode Core category count / cap
- active truth-source:
  - `./run_tests.sh core`
  - `./run_tests.sh full`
  - pinned baseline doc
  - touched cluster readout
  - grep gate for removed structural vocabulary
  - semantic query migration source-scan gate
- 규칙 정의 위치:
  - `Tools/gameplay_test_stratification_lib.py`
  - `Tools/check_gameplay_semantic_query_migration.py`
- 규칙 소비 위치:
  - `Tools/check_gameplay_test_stratification.py`
  - `Tools/generate_gameplay_test_stratification.py --check`
  - `Tools/semantic_query_migration_allowlist.json`
- runner integration:
  - `run_tests.sh`는 checker를 실행하지만 `gameplay_test_stratification_lib.py`를 직접 import하지 않는다.
  - `TestRunnerCliBootstrap`의 Core PlayMode selection은 persisted manifest가 아니라 `assemblyNames + categoryNames("Core")`를 사용한다.
- historical/non-canonical:
  - [Docs/Archive/Architecture/Gameplay-Test-Stratification.md](../Archive/Architecture/Gameplay-Test-Stratification.md)
- 모드:
  - 로컬 기본값: `soft`
  - CI 기본값: `strict`
  - 명시적 override: `STRATIFICATION_GOVERNANCE_MODE=soft|strict`

### English Original
- Governance checks:
  - test placement boundaries
  - Core purity rules
  - gameplay semantic query migration boundary
  - execution-based tests outside Integration
  - source category / override inventory consistency
  - PlayMode Core category count / cap
- active truth sources:
  - `./run_tests.sh core`
  - `./run_tests.sh full`
  - the pinned baseline doc
  - touched-cluster readouts
  - grep gates for removed structural vocabulary
  - the semantic query migration source-scan gate
- Rule source:
  - `Tools/gameplay_test_stratification_lib.py`
  - `Tools/check_gameplay_semantic_query_migration.py`
- Rule consumers:
  - `Tools/check_gameplay_test_stratification.py`
  - `Tools/generate_gameplay_test_stratification.py --check`
  - `Tools/semantic_query_migration_allowlist.json`
- Runtime wiring stage note:
  - `Tools/semantic_query_migration_allowlist.json` is expected to be empty after post-semantic-migration runtime wiring lands.
  - Any new entry is a temporary quarantine and must be removed in the same staged migration thread that introduced it.
- Runner integration:
  - `run_tests.sh` invokes the checker but no longer imports `gameplay_test_stratification_lib.py` directly.
  - `TestRunnerCliBootstrap` now uses `assemblyNames + categoryNames("Core")` for Core PlayMode selection instead of a persisted manifest.
- Historical/non-canonical:
  - [Docs/Archive/Architecture/Gameplay-Test-Stratification.md](../Archive/Architecture/Gameplay-Test-Stratification.md)
- Modes:
  - local default: `soft`
  - CI default: `strict`
  - explicit override: `STRATIFICATION_GOVERNANCE_MODE=soft|strict`

## 8. Governance Philosophy / Governance 철학
### 한국어
- Governance는 자동 경계 강제 시스템이다.
- 개발자가 모든 테스트의 위치를 수동으로 기억할 필요가 없도록 만드는 것이 목적이다.
- 시스템은 다음을 유지하기 위해 존재한다.
  - Core는 순수하게
  - Integration은 동작 중심으로
  - Infrastructure는 구조 중심으로
- Governance 실패에 대한 기본 대응은 규칙 약화가 아니라 테스트 이동 또는 분리다.

### English Original
- Governance is automatic boundary enforcement.
- Developers should not have to manually remember where every test belongs.
- The system exists to keep:
  - Core pure
  - Integration behavioral
  - Infrastructure structural
- The correct response to a governance failure is usually to move or split a test, not to weaken the rule.

## 9. Test Classification Rules / 테스트 분류 규칙
### 한국어
- `Core`
  - 결정적 입력/출력 로직만 허용
  - 파이프라인 실행 금지
  - composition root 또는 bootstrap wiring 금지
  - non-public reflection 금지
- `Infrastructure`
  - 구조/시그니처/리플렉션 전용
  - 게임플레이 시뮬레이션 금지
  - 런타임 동작 결과 검증 금지
- `Integration`
  - 런타임 실행 포함
  - 다중 시스템 동작 포함
  - 파이프라인 흐름, tick progression, 조합된 게임플레이 시나리오 포함

### English Original
- `Core`
  - deterministic input/output logic only
  - no pipeline execution
  - no composition root or bootstrap wiring
  - no non-public reflection
- `Infrastructure`
  - structure/signature/reflection only
  - no gameplay simulation
  - no runtime behavior assertions
- `Integration`
  - runtime execution
  - multi-system behavior
  - pipeline flow, tick progression, and composed gameplay scenarios

## 10. Test Classification Decision Table / 테스트 분류 결정표
### 한국어
| 테스트가 다음에 해당하면... | 배치 위치 |
| --- | --- |
| `RunTick`, `TickPipeline`, `TickRunner`, `CreateTickPipeline`, `CreateTickRunner`를 호출한다 | `Integration` |
| `BindingFlags`, `GetFields`, `GetConstructors`, 생성자 검사, DI/wiring 검사, composition 검사를 사용한다 | `Infrastructure` |
| 결정적 입력/출력만 검증한다 | `Core` |

- 애매하면 `Core`보다 `Integration`을 우선한다.

### English Original
| If the test... | Put it in... |
| --- | --- |
| Calls `RunTick`, `TickPipeline`, `TickRunner`, `CreateTickPipeline`, or `CreateTickRunner` | `Integration` |
| Uses `BindingFlags`, `GetFields`, `GetConstructors`, constructor checks, DI/wiring checks, or composition checks | `Infrastructure` |
| Validates deterministic input/output only | `Core` |

- When in doubt, prefer `Integration` over `Core`.

## 11. Ambiguous Cases / 애매한 경우 처리
### 한국어
- 실행 + 구조 검증이 섞인 테스트
  - 반드시 분리한다. 실행은 Integration, 구조/wiring은 Infrastructure에 둔다.
- helper 함수가 실행을 숨기는 경우
  - helper를 경유해도 분류는 바뀌지 않는다.
  - helper가 결국 pipeline flow를 실행하면 그 테스트는 execution-based test다.
- 부분 파이프라인 실행
  - 전체 시나리오가 아니어도 실행이면 실행이다.
  - 일부 phase만 실행해도 Integration이다.
- 강한 규칙:
  - 확신이 없으면 pure하다고 입증되기 전까지 `Integration`으로 분류한다.

### English Original
- Mixed execution + structure test
  - Split it. Execution stays in Integration. Structure/wiring stays in Infrastructure.
- Helper functions hiding execution
  - Helper indirection does not change classification.
  - If a helper eventually runs pipeline flow, the test is still execution-based.
- Partial pipeline execution
  - Partial execution is still execution.
  - A test does not need a full scenario to qualify as Integration.
- Hard rule:
  - If unclear, treat the test as `Integration` until proven pure.

## 12. Migration Rules / 테스트 이동 규칙
### 한국어
- 실행 테스트 -> `Integration`
- reflection / constructor / DI / composition 테스트 -> `Infrastructure`
- 결정적 로직 테스트 -> `Core`
- mixed test는 허용되지 않는다.
- 하나의 테스트가 런타임 실행과 구조 검증을 동시에 수행하면:
  - 동작 검증은 Integration으로
  - 구조 검증은 Infrastructure로 분리한다.

### English Original
- Execution test -> `Integration`
- Reflection / constructor / DI / composition test -> `Infrastructure`
- Deterministic logic test -> `Core`
- No mixed tests allowed.
- If one test mixes runtime execution and structure validation:
  - split the behavior assertions into Integration
  - split the structure assertions into Infrastructure

## 13. Real Examples / 실제 예시
### 한국어
#### Before
- 하나의 테스트가 `RunTick`을 호출하고 동시에 reflection으로 constructor shape도 검사한다.

#### After
- `Integration` test
  - `RunTick`을 실행한다.
  - 결과 동작, 상태, 이벤트 로그, phase output을 검증한다.
- `Infrastructure` test
  - 생성자 공개 여부, provider wiring, type shape를 검증한다.
- `Core` test
  - 순수 comparer, normalizer, ordering rule, buffer behavior를 결정적 입력으로 검증한다.

### English Original
#### Before
- One test calls `RunTick` and also checks constructor shape with reflection.

#### After
- `Integration` test
  - runs `RunTick`
  - verifies resulting behavior, state, event log, or phase output
- `Infrastructure` test
  - checks constructor visibility, provider wiring, or type shape
- `Core` test
  - validates a pure comparer, normalizer, ordering rule, or buffer behavior with deterministic inputs

## 14. Failure Output & Debugging / 실패 출력과 디버깅
### 한국어
- 테스트 실행이 성공으로 인정되려면 XML은 다음을 모두 만족해야 한다.
  - 파일이 존재한다.
  - 비어 있지 않다.
  - `<test-run`을 포함한다.
  - `total="..."`을 포함한다.
  - 총 테스트 수가 0이 아니다.
- shell은 다음을 출력한다.
  - 실패한 테스트 이름
  - 첫 줄 실패 이유
  - stage별 metric
- 로그와 결과 파일은 `TestResults/` 아래에 기록된다.
- 일반적으로 확인하는 파일:
  - Unity 로그
  - Unity XML 결과
  - Windows `dotnet` 로그

#### Failure Types
- `Core failure`
  - 결정적 로직 회귀
  - 즉시 수정해야 한다.
- `Integration failure`
  - 런타임 또는 시스템 동작 문제
- `Infrastructure failure`
  - 구조, 시그니처, 생성자, wiring 계약 위반

### English Original
- A test run is only accepted when the XML:
  - exists
  - is non-empty
  - contains `<test-run`
  - contains `total="..."`
  - reports a nonzero test count
- The shell prints:
  - failed test names
  - first-line failure reasons
  - per-stage metrics
- Logs and results are written under `TestResults/`.
- Typical files include:
  - Unity logs
  - Unity XML results
  - Windows `dotnet` logs

#### Failure Types
- `Core failure`
  - deterministic logic regression
  - fix immediately
- `Integration failure`
  - runtime/system behavior issue
- `Infrastructure failure`
  - architecture, signature, constructor, or wiring contract violation

## 15. Interpreting Governance Failures / Governance 실패 해석
### 한국어
- Governance 실패는 보통 “테스트가 잘못된 계층에 있다”는 뜻이다.
- 이것이 곧 테스트 대상 로직이 틀렸다는 뜻은 아니다.
- 기본 대응 순서:
  - 테스트를 이동한다.
  - 테스트를 분리한다.
  - 그 뒤에도 실패하면, 그때 로직 문제를 다시 본다.

### English Original
- Governance failure means the test is in the wrong layer.
- It does not necessarily mean the tested logic is wrong.
- Expected response:
  - move the test
  - split the test
  - only then revisit logic if the test is correctly placed and still failing

## 16. Metrics & Reliability Signals / 메트릭과 신뢰성 신호
### 한국어
- 각 Unity stage는 다음을 출력한다.
  - 총 테스트 수
  - 실패 테스트 수
  - failure ratio
  - duration seconds
- 자동화는 다음과 같은 의심스러운 실행 패턴에 대해 warning을 낸다.
  - 실행된 테스트 수가 예상보다 급감함
  - 최근 성공 실행 대비 비정상적으로 짧은 실행 시간
- 이 warning은 “로직 버그 확정”이 아니라 “실행 신뢰성 신호”다.

### English Original
- Each Unity stage prints:
  - total tests
  - failed tests
  - failure ratio
  - duration in seconds
- The automation also emits warnings for suspicious runs, such as:
  - unexpected drops in executed test count
  - unexpectedly short duration compared with recent successful runs
- These warnings are reliability signals, not automatic proof of a logic bug.

## 17. Current Known Limitations / 현재 알려진 제한 사항
### 한국어
- PlayMode는 아직 완전히 stratified되지 않았다.
- Full 실행은 Core 게이트 밖의 런타임 문제나 에셋 문제로 여전히 실패할 수 있다.
- `core`만이 일상 개발에서 사용하는 엄격한 게이트다.
- 이것은 의도된 설계다.
  - Core는 개발 속도를 최적화한다.
  - Full은 더 넓은 검증 경로로 남는다.

### English Original
- PlayMode is not fully stratified yet.
- Full runs may still fail because of runtime or asset issues outside the Core gate.
- `core` is the only strict day-to-day gate.
- This is intentional:
  - Core optimizes developer velocity
  - Full remains the broader validation path

## 18. Recommended Workflow / 권장 워크플로
### 한국어
1. `./run_tests.sh core`를 자주 실행한다.
2. 실패하면 먼저 실패 유형을 분류한다.
3. 로직 버그를 수정하거나 잘못 배치된 테스트를 이동/분리한다.
4. `core`를 다시 실행한다.
5. 주기적으로, 그리고 안정화/통합 전에 `full`을 실행한다.

- Core 실패는 미루지 않는다.
- 리팩터링 중 Full 실패는 즉시 차단 신호라기보다 참고 신호가 될 수 있다.

### English Original
1. Run `./run_tests.sh core` frequently.
2. If it fails, classify the failure first.
3. Fix the logic bug or move/split the misplaced test.
4. Rerun `core`.
5. Run `full` periodically and before stabilization or integration.

- Core failures should not be deferred.
- Full failures during active refactors can be informative without blocking local progress.

## 19. Do / Don’t Rules / 해야 할 것과 하지 말아야 할 것
### 한국어
#### Do
- Core를 결정적으로 유지한다.
- mixed test를 분리한다.
- execution test를 Integration으로 이동한다.
- governance failure를 먼저 배치 문제로 해석한다.

#### Don’t
- execution test를 Core에 넣지 않는다.
- reflection-only test를 Integration에 넣지 않는다.
- category filtering을 실행 모델로 기대하지 않는다.
- classification을 고치지 않고 governance를 우회하지 않는다.

### English Original
#### Do
- Keep Core deterministic.
- Split mixed tests.
- Move execution tests into Integration.
- Treat governance failures as placement problems first.

#### Don’t
- Put execution tests in Core.
- Put reflection-only tests in Integration.
- Rely on category filtering as the execution model.
- Bypass governance instead of fixing classification.

## 20. Future Evolution / 향후 발전 방향
### 한국어
- PlayMode stratification의 추가 정교화
- Integration 범위의 확장
- internal integration selection을 활용한 CI 병렬화

### English Original
- Fuller PlayMode stratification.
- Broader Integration expansion.
- CI parallelization through internal integration selections.

> Final principle: This guide is the operational source of truth for gameplay test execution and structure.
>
> 최종 원칙: 이 가이드는 게임플레이 테스트 실행과 구조에 대한 운영상의 단일 기준 문서다.
