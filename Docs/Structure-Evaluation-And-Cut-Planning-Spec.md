# 정밀 구조 평가 및 후속 절단 계획 스펙 보강안

> Supporting workflow spec.
> 이 문서는 구조 평가 문서와 후속 절단 계획 문서의 작성 규칙을 고정하는 지원 문서다.
> canonical architecture truth-source를 대체하지 않으며, 구현/규칙/테스트 운영의 기준은 계속 [Docs/Architecture/README.md](./Architecture/README.md)와 [Docs/Testing/Gameplay-Test-Automation-Guide.md](./Testing/Gameplay-Test-Automation-Guide.md), [Docs/Testing/UI-EditMode-Baseline-2026-04-15.md](./Testing/UI-EditMode-Baseline-2026-04-15.md), [Docs/Testing/Full-EditMode-Baseline-2026-04-13.md](./Testing/Full-EditMode-Baseline-2026-04-13.md), [Docs/DeferredStaleLedger.md](./DeferredStaleLedger.md)를 따른다.

## Summary

- 기존 스펙의 방향은 유지한다. `Pipeline / Enemy AI / Input`은 구현 집중 리스크가 크고, `UI`는 구조적으로 강하지만 composition/helper 중앙집중 리스크가 있으며, `Audio`는 가장 건강한 영역으로 본다.
- 이번 보강의 목적은 형식을 더 늘리는 것이 아니라, 같은 형식 안에서 `우선순위`, `착수 조건`, `증거 부족`을 더 재현 가능하게 만드는 것이다.
- 문서 출력 스키마에는 기존 항목 외에 반드시 `핵심 결론 문장`, `priority tier`, `primary lane`, `secondary lane`, `risk note`, `evidence status`, `candidate bucket`을 추가한다.
- 기본값은 기존 evidence만 쓰되, 경계선 타입에는 lane 판정과 절단 축을 잠그기 위한 제한적 targeted evidence를 예외적으로 허용한다.

## 1. 유지할 기존 판단

- 모든 판단은 `정적 구조 리스크`와 `실제 변경/회귀 리스크`를 분리해서 쓴다.
- public seam은 유지하고, stale/test drift 때문에 runtime rollback을 제안하지 않는다.
- lane은 `runtime/core`, `composition/installer`, `authoring/prefab/view-factory`, `test-contract / stale expectation`으로 나눈다.
- 큰 파일이라는 이유만으로 구조 실패를 선언하지 않고, `의도된 중앙집중`과 `실제 책임 혼합`을 분리해서 본다.
- `Audio`는 저우선 건강 영역으로 유지하되, `Pipeline`, `Enemy AI`, `Input`, 일부 `UI` composition 타입은 절단 기준을 먼저 명확히 해야 하는 영역으로 유지한다.

## 2. 보강된 스펙 원칙

### 형식 vs 실질 판단 구분 규칙

- 산출물 평가는 `Format Gate`, `Judgment Gate`, `Execution Gate` 세 단계로 본다.
- `Format Gate`는 섹션 순서, 필수 항목, 타입별 판정 형식이 맞는지만 본다.
- `Judgment Gate`는 각 영역과 타입마다 적어도 한 문장으로 `무엇이 실제 우선 문제인지`, `무엇은 아직 증거 부족인지`, `그래서 지금 무엇을 해야 하는지`가 분명해야 통과한다.
- `Execution Gate`는 최종 문서가 `즉시 착수 후보`, `evidence 선행 후보`, `보류 후보`를 실제로 분리해 구현 순서 잠금에 연결할 수 있어야 통과한다.
- 형식은 맞지만 통찰이 약한 산출물은 아래 중 하나라도 있으면 탈락시킨다.
  - 모든 항목이 같은 말의 반복만 한다.
  - `priority tier`가 규칙에 매핑되지 않는다.
  - `유지 seam` 또는 `금지 사항`이 없다.
  - 절단 기준 대신 파일 크기만 반복한다.
  - `candidate bucket`이 있지만 왜 그 bucket인지 설명이 없다.
- 각 영역과 각 핵심 타입에는 반드시 한 문장의 `핵심 결론 문장`을 넣는다.
- 핵심 결론 문장 형식은 고정한다.
  - `현재 우선순위는 [tier]다. 이유는 [구조 신호]와 [변경 신호] 때문이며, [부족한 evidence]가 없어 여기까지만 단정한다.`

### 필수 출력 메타필드

- 최종 평가 문서의 각 영역 또는 핵심 타입 블록은 아래 메타필드를 반드시 함께 남긴다.
  - `핵심 결론 문장`
  - `priority tier`
  - `primary lane`
  - `secondary lane`
  - `risk note`
  - `evidence status`
  - `candidate bucket`
- `secondary lane`과 `risk note`는 항상 둘 다 필요한 것이 아니다.
  - adjacent pressure가 명확하면 `secondary lane`을 적는다.
  - lane 둘만으로 혼합 리스크가 충분히 설명되지 않으면 `risk note`를 적는다.
- `evidence status`는 아래 네 값으로 고정한다.
  - `existing-only`
    - 기존 구조 guard, baseline, stale ledger만으로 priority와 cut axis를 설명할 수 있다.
  - `targeted-needed`
    - lane 또는 cut axis를 잠그기 위해 질문 하나짜리 targeted evidence가 더 필요하다.
  - `targeted-complete`
    - 제한적 targeted evidence를 이미 반영했고, 추가 broad evidence sweep 없이 판정 가능하다.
  - `insufficient`
    - 정적 리스크는 보이지만 현재 evidence로는 strong priority를 잠글 수 없다.
- `candidate bucket`은 아래 셋 중 하나로만 적는다.
  - `즉시 착수 후보`
  - `evidence 선행 후보`
  - `보류 후보`
- 메타필드가 채워져 있어도 아래 중 하나면 `Judgment Gate`를 통과하지 못한다.
  - `priority tier`와 `candidate bucket`의 연결 근거가 없다.
  - `evidence status`가 있는데 추가 질문 또는 existing evidence 범위가 적혀 있지 않다.
  - `primary lane`은 적었지만 왜 그 lane이 first-cut owner인지 설명이 없다.
  - `핵심 결론 문장`이 영역/타입마다 달라지지 않고 같은 문장을 반복한다.

### 우선순위 판단 규칙

- 최종 문서는 `High / Medium / Low / Evidence-hold` 단계형 결과를 사용한다.
- 단계형 결과 뒤에는 내부 판정용 `5-signal worksheet`를 근거로 남긴다.
- 점수는 본문에 모두 드러내지 않아도 되지만, 각 결론은 어떤 신호를 밟았는지 재현 가능해야 한다.

#### 5-signal worksheet

아래 다섯 요소를 각 `0 / 1 / 2`로 본다.

- `touched cluster`
  - `0`: current baseline touched cluster와 직접 관련이 없다.
  - `1`: adjacent cluster 또는 간접 영향만 보인다.
  - `2`: touched cluster에 직접 포함되거나 바로 연결된다.
- `stale ledger / lane row`
  - `0`: stale ledger나 lane 문서에 관련 row가 없다.
  - `1`: adjacent drift 또는 주변 타입 row만 있다.
  - `2`: 해당 타입/영역과 직접 연결된 stale row가 있다.
- `multi-surface pressure`
  - `0`: guard 또는 단일 surface만 압박한다.
  - `1`: guard + one runtime surface 조합이다.
  - `2`: 구조 guard + scenario/replay/playmode smoke가 함께 압박한다.
- `blast radius`
  - `0`: 수정 영향이 local test/class 범위에 그친다.
  - `1`: 같은 모듈 내 여러 test class로 넓어진다.
  - `2`: cross-module 또는 frozen baseline까지 재검증이 필요하다.
- `contract revalidation`
  - `0`: bootstrap/prefab/asset contract 재검증이 거의 없다.
  - `1`: 제한적 재검증이 필요하다.
  - `2`: bootstrap/prefab/asset contract 재검증이 핵심 비용이다.

#### priority tier mapping

- `High priority`
  - 총점 `6+`이면서 `multi-surface pressure` 또는 `blast radius`가 `2`다.
  - 또는 총점 `5`라도 `touched cluster = 2`와 `contract revalidation = 2`가 동시에 있다.
  - 또는 `primary lane`이 명확한데 baseline freeze와 authoritative scenario pressure를 동시에 받는다.
- `Medium priority`
  - 총점 `3~5`다.
  - 정적 구조 리스크는 분명하지만, 변경 압력이 한두 surface에만 걸린다.
  - 절단 축은 보이지만 `즉시 착수`로 잠글 만큼 evidence가 충분하지 않다.
- `Low priority`
  - 총점 `1~2`다.
  - 정적 응집은 보이나 public seam과 authoritative behavior pressure가 약하다.
  - 현재 중앙집중이 의도된 orchestration으로 기능하고 있고 재검증 범위도 좁다.
- `Evidence-hold`
  - 정적 리스크는 보이지만 점수 근거가 약하다.
  - lane 애매성이 cross-lane mixed risk가 아니라 evidence 부족 때문이다.
  - stale row나 contract drift가 있어도 runtime regression과 연결된 확증이 없다.

- priority 결론은 정성 문장만 쓰지 않는다.
- 각 결론 뒤에 최소한 `touched/stale/multi-surface/blast-radius/contract-check` 중 어떤 항목이 실제로 걸렸는지 명시한다.

### evidence 기본값/예외 규칙

- 기본값은 기존 evidence만 사용한다.
- 기본 evidence source는 다음으로 고정한다.
  - 구조 guard
  - contract test
  - scenario/replay/playmode smoke의 existing baseline
  - stale ledger와 dated lane 문서
- 현재 저장소 기준의 대표 anchor는 아래 문서를 우선 참조한다.
  - [Docs/Architecture/README.md](./Architecture/README.md)
  - [Docs/Testing/Gameplay-Test-Automation-Guide.md](./Testing/Gameplay-Test-Automation-Guide.md)
  - [Docs/Testing/UI-EditMode-Baseline-2026-04-15.md](./Testing/UI-EditMode-Baseline-2026-04-15.md)
  - [Docs/Testing/Full-EditMode-Baseline-2026-04-13.md](./Testing/Full-EditMode-Baseline-2026-04-13.md)
  - [Docs/DeferredStaleLedger.md](./DeferredStaleLedger.md)
  - [Docs/Testing/Remaining-55-Non-Runtime-Lane-Lock-Refinement-2026-04-18.md](./Testing/Remaining-55-Non-Runtime-Lane-Lock-Refinement-2026-04-18.md)
  - [Docs/Testing/Remaining-56-Lane-Reclassification-2026-04-18.md](./Testing/Remaining-56-Lane-Reclassification-2026-04-18.md)
- 예외적으로 targeted evidence를 더 모아도 되는 경우는 `경계선 타입의 primary lane 또는 cut axis를 잠글 수 없는 경우`로 제한한다.
- targeted evidence는 반드시 질문 하나만 해결해야 한다.
  - `runtime/core 집중인가, composition adjunct인가`
  - `authoring contract pressure가 실제 주 원인인가`
  - `동시 압박이 실제 있는가`
- targeted evidence는 non-mutating classification 작업으로 제한한다.
  - targeted readout
  - targeted smoke interpretation
  - existing suite 결과 재분류
- targeted evidence를 모으는 과정에서 새로운 구조 방향이나 rollback 결론을 먼저 잠그면 안 된다.
- 예외 후보 타입은 아래로 고정한다.
  - `TickPipeline`
  - `GameplayUiFlowInstaller`
  - `GameplayScreenRuntimeFactory`
  - `UIFlowCoordinator`
  - `EnemyLogic`
  - `EnemyMovementPolicy`
  - `GameplayInputHost`
- `TickResultBuilder`와 `AudioManager`는 기본적으로 기존 evidence만 쓰고, public seam drift나 consumer pressure가 실제로 보일 때만 예외 후보로 올린다.
- 새 evidence를 허용해도 보호 기준은 유지한다.
  - lane 혼합 금지
  - runtime rollback 금지
  - public seam 확장 금지
  - targeted evidence는 classification용이지 refactor 정당화용이 아니다
  - multi-lane broad evidence sweep 금지

### lane 분류 유연화 규칙

- 모든 타입은 `primary lane`을 반드시 가진다.
- `secondary lane`은 인접 lane의 계약이나 조립 압력이 분명할 때만 붙인다.
- `risk note`는 lane이 두 개 이상이라서가 아니라, `주 lane은 명확하지만 그대로 적으면 혼합 리스크가 과소표현되는 경우`에 붙인다.
- `risk note`가 필요한 대표 상황은 다음과 같다.
  - composition root가 input/debug adjunct를 품는 경우
  - runtime core 타입이 authoring compiler special-case를 품는 경우
  - factory가 prefab contract와 runtime orchestration을 동시에 묶는 경우
- secondary lane이 없다고 해서 무조건 단순 타입은 아니다.
- lane이 애매한 이유가 cross-lane이 아니라 evidence 부족이면 `secondary lane` 대신 `Evidence-hold`로 남긴다.
- lane 혼합과 evidence 부족을 혼동하지 않는다.
  - 이미 경계가 보이는데 두 lane 모두 실질 압력이 있으면 mixed risk다.
  - 경계 자체가 아직 evidence 부족이면 hold다.

### 실행성 강화 규칙

- 문서 마지막에는 반드시 `즉시 착수 후보`, `evidence 선행 후보`, `보류 후보`를 남긴다.
- `즉시 착수 후보`
  - `High`다.
  - seam과 cut axis가 이미 명확하다.
  - 추가 targeted evidence 없이 lane을 잠글 수 있다.
- `evidence 선행 후보`
  - `High` 또는 `Medium`이다.
  - lane 또는 cut axis를 잠그기 위한 targeted evidence 질문이 하나 남아 있다.
- `보류 후보`
  - `Low` 또는 `Evidence-hold`다.
  - 또는 건강한 중앙집중이라 당장 절단 비용보다 유지 비용이 낮다.
- 구현 순서 잠금은 `priority tier`만으로 하지 않는다.
- `candidate bucket`과 `건드리면 안 되는 seam`이 함께 명시돼야 실제 착수 대상으로 올린다.

#### 최종 문서 말미 집계 블록

- 최종 평가 문서의 마지막에는 아래 세 블록을 반드시 순서대로 둔다.
  - `즉시 착수 후보`
  - `evidence 선행 후보`
  - `보류 후보`
- 각 항목은 최소한 아래 다섯 줄을 함께 남긴다.
  - `priority tier`
  - `primary lane`
  - `evidence status`
  - `유지 seam`
  - `first-cut axis`
- `evidence 선행 후보`에는 아래 항목을 추가한다.
  - `풀어야 할 질문 1개`
  - `허용되는 targeted evidence`
- `보류 후보`에는 아래 항목을 추가한다.
  - `보류 이유`
  - `재평가 트리거`
- 최종 집계 블록은 단순 목록이 아니라 `왜 지금 시작하는지 / 왜 더 기다리는지`가 드러나는 실행 backlog 잠금 장치여야 한다.

## 3. 타입별 판정 보강 규칙

### TickPipeline

- 유지할 기존 판정
  - public/runtime orchestration은 의도된 중앙집중이다.
  - internal phase payload/result/finalization까지 한 파일에 응집된 부분은 실제 책임 혼합으로 본다.
- 보강할 판정 기준
  - `primary lane`: `runtime/core`
  - `priority tier`는 `blast radius`와 `multi-surface pressure`가 클 때 High 쪽으로 본다.
  - 파일 크기만으로는 부족하고, phase-private carrier 변화가 scenario/replay/guard를 함께 흔드는지가 핵심이다.
- evidence 부족 시 보수적으로 남길 부분
  - phase order 자체
  - `TickRunner` monotonic semantics
  - `TickResult.PresentationData`
  - `TickResult.FinalTopology`
- 예외적으로 추가 evidence를 모아도 되는 조건
  - guard는 강한데 실제 scenario/replay 압박이 불명확할 때
  - targeted scenario/replay cluster readout이 `runtime/core` cut axis 하나를 잠그는 데만 필요할 때

### TickResultBuilder

- 유지할 기존 판정
  - result projection 중앙집중은 허용된다.
  - event aggregation, presentation build, internal carrier 정의가 한 파일에 응집된 것은 SRP 혼합 신호다.
- 보강할 판정 기준
  - `primary lane`: `runtime/core`
  - consumer seam pressure가 실제로 있을 때만 우선순위를 올린다.
  - build helper 응집만 있고 consumer drift가 없으면 Medium 이하로 둔다.
- evidence 부족 시 보수적으로 남길 부분
  - `TickResult` public surface
  - presentation seam projection ownership
- 예외적으로 추가 evidence를 모아도 되는 조건
  - view/audio consumer cluster 또는 presentation isolation test가 동시에 흔들리는 정황이 있을 때

### GameplayUiFlowInstaller

- 유지할 기존 판정
  - UI composition root와 gameplay-host bridge 역할은 의도된 중앙집중이다.
  - keyboard shortcut, diagnostics toggle, wiring lifecycle이 함께 붙은 부분은 실제 책임 혼합이다.
- 보강할 판정 기준
  - `primary lane`: `composition/installer`
  - `risk note`: `input/debug adjunct contamination`
  - `UI baseline freeze + TutorialScene smoke`가 함께 걸리면 High 또는 evidence-first로 본다.
  - composition ownership 자체가 아니라 adjunct 오염 여부를 본다.
- evidence 부족 시 보수적으로 남길 부분
  - canonical installer
  - root-shell composition
  - host bridge 자체
- 예외적으로 추가 evidence를 모아도 되는 조건
  - diagnostics/input adjunct가 실제 runtime ownership pressure인지 모호할 때
  - targeted UI smoke와 installer integration evidence가 질문 하나만 푸는 데 쓰일 때

### GameplayScreenRuntimeFactory

- 유지할 기존 판정
  - screen runtime ownership centralization은 의도된 것이다.
  - screen별 presenter/view/policy assembly를 중앙 switch에 누적시키는 구조는 authoring/composition 혼합 리스크다.
- 보강할 판정 기준
  - `primary lane`: `authoring/prefab/view-factory`
  - `secondary lane`: `composition/installer`
  - `screen contract revalidation`과 `blast radius`가 크면 High, 아니면 evidence-first로 둔다.
- evidence 부족 시 보수적으로 남길 부분
  - `ScreenController` ownership
  - canonical screen runtime path
- 예외적으로 추가 evidence를 모아도 되는 조건
  - 문제의 중심이 runtime ownership인지 prefab contract churn인지 모호할 때
  - screen prefab contract guard와 screen flow tests가 한 질문만 푸는 데 쓰일 때

### UIFlowCoordinator

- 유지할 기존 판정
  - popup-first back handling과 root/screen/popup sequencing은 의도된 coordination이다.
  - 분기 누적이 커질 때만 실제 책임 혼합으로 본다.
- 보강할 판정 기준
  - `primary lane`: `composition/installer`
  - branch growth evidence가 없으면 기본값은 Medium 또는 Low다.
  - stage-clear, popup-first-back, cross-layer sequencing이 동시에 흔들릴 때만 우선순위를 올린다.
- evidence 부족 시 보수적으로 남길 부분
  - coordinator 자체 존재
  - sequencing ownership
- 예외적으로 추가 evidence를 모아도 되는 조건
  - stage-clear routing과 screen/popup sequencing pressure가 실제로 coordinator에 집중되는지 불명확할 때

### EnemyLogic

- 유지할 기존 판정
  - AI transition, pre-movement, movement intent, attack intent를 동시에 가진 것은 실제 runtime-core 책임 혼합이다.
- 보강할 판정 기준
  - `primary lane`: `runtime/core`
  - `Enemy AI scenario`와 touched cluster 압력이 크면 High로 본다.
  - authoring OCP는 별도 강점으로 분리하고, runtime implementation concentration을 따로 본다.
- evidence 부족 시 보수적으로 남길 부분
  - compiled profile seam
  - capability-based authoring path
- 예외적으로 추가 evidence를 모아도 되는 조건
  - runtime-core 절단 축이 `transition vs intent vs capability binding` 중 어디인지 모호할 때
  - targeted AI scenario evidence가 질문 하나만 푸는 데 쓰일 때

### EnemyMovementPolicy

- 유지할 기존 판정
  - strategy vocabulary는 좋다.
  - `EnemyMovementStrategyShared`와 `EnemyChargeStrategyShared`로 규칙이 다시 blob화된 부분은 OCP 약화 신호다.
- 보강할 판정 기준
  - `primary lane`: `runtime/core`
  - `risk note`: `authoring vocabulary stays healthy but runtime strategy implementation re-centralizes`
  - 실제 priority는 strategy 확장 시 central helper 수정이 강제되는지로 판단한다.
- evidence 부족 시 보수적으로 남길 부분
  - strategy asset vocabulary
  - patrol/chase kind surface
- 예외적으로 추가 evidence를 모아도 되는 조건
  - shared algorithm blob이 실제 scenario blast radius를 일으키는지 불명확할 때
  - pathfinding/chase 관련 targeted unit/scenario evidence가 한 질문만 푸는 데 쓰일 때

### AudioManager

- 유지할 기존 판정
  - audio infra central runtime은 의도된 중앙집중이다.
  - 기본값은 건강한 영역 쪽이다.
  - nested registry/pool/controller 응집은 infra-local issue다.
- 보강할 판정 기준
  - `primary lane`: `runtime/core`
  - 기본 `priority tier`는 Low다.
  - public audio contract drift, UI seam reuse, gameplay-domain leakage가 없으면 우선순위를 올리지 않는다.
- evidence 부족 시 보수적으로 남길 부분
  - `IAudioService`
  - `AudioRuntimeInstaller`
  - `AudioRuntimeRoot`
  - `GameplayAudioPresenter -> GameplayAudioMap`
- 예외적으로 추가 evidence를 모아도 되는 조건
  - contract drift나 domain leakage가 실제로 포착됐을 때만 targeted audio architecture evidence를 추가한다.

### GameplayInputHost

- 유지할 기존 판정
  - Unity input binding + tick host entrypoint는 의도된 host orchestration이다.
  - binding, buffering, UI override, pause, tick advancement, command creation을 동시에 품는 것은 실제 책임 혼합이다.
- 보강할 판정 기준
  - `primary lane`: `runtime/core`
  - `secondary lane`: `composition/installer`
  - `risk note`: `host adjunct logic can obscure runtime-core cut axis`
  - `touched cluster`, `blast radius`, `playmode smoke`가 함께 걸리면 High로 본다.
- evidence 부족 시 보수적으로 남길 부분
  - `PlayerTickCommand` carrier
  - `TickRunner` monotonic path
  - UI direct-authoritative write 금지 경계
- 예외적으로 추가 evidence를 모아도 되는 조건
  - runtime-core 절단인지 host adjunct 절단인지 모호할 때
  - input unit + canonical playmode smoke가 질문 하나만 푸는 데 쓰일 때

## 4. lane 분류 보강 규칙

### lane field semantics

- `primary lane`
  - 실제 first-cut backlog를 배정할 주 lane이다.
  - authoritative owner와 main change cost가 있는 곳이어야 한다.
- `secondary lane`
  - adjacent contract 또는 assembly pressure가 분명할 때만 적는다.
  - 실행 backlog의 주 소유권을 빼앗지 않는다.
- `risk note`
  - lane이 둘 이상이라는 뜻이 아니라, lane 설명만으로는 놓치기 쉬운 혼합 리스크를 짧게 적는 필드다.

### runtime/core

- primary 판단 기준
  - authoritative semantics
  - phase ownership
  - command/intention/state update
  - scenario/replay 영향
- secondary 판단 기준
  - authoring/compiler나 installer와 연결되더라도 핵심 cut axis가 runtime behavior면 primary는 유지한다.
- risk note가 필요한 경우
  - runtime core 타입이 authoring special-case나 host adjunct를 품어 lane 설명이 과소해질 때
- 구현 lane으로 올리기 전 추가 evidence가 필요한 경우
  - scenario/replay 압력은 있는데 cut axis가 아직 `state / intent / finalize / projection` 중 어디인지 불명확할 때

### composition/installer

- primary 판단 기준
  - root composition
  - lifecycle sequencing
  - cross-layer routing
  - host bridge
  - installer-owned diagnostics/input adjunct
- secondary 판단 기준
  - runtime semantics를 읽더라도 authoritative mutation을 소유하지 않으면 composition으로 둔다.
- risk note가 필요한 경우
  - composition root가 screen/popup policy 세부나 input/debug behavior까지 흡수했을 때
- 구현 lane으로 올리기 전 추가 evidence가 필요한 경우
  - 문제의 중심이 truly composition인지, runtime behavior bug가 composition에서 보이는 것뿐인지 아직 구분이 안 될 때

### authoring/prefab/view-factory

- primary 판단 기준
  - prefab/catalog/factory assembly
  - asset contract
  - authoring-to-runtime compile seam
  - view factory shape
- secondary 판단 기준
  - composition 또는 runtime와 연결되더라도 재검증 비용의 중심이 prefab/asset contract면 primary는 유지한다.
- risk note가 필요한 경우
  - 중앙 factory가 runtime orchestration과 prefab contract churn을 동시에 묶어 first-cut backlog가 왜곡될 때
- 구현 lane으로 올리기 전 추가 evidence가 필요한 경우
  - prefab contract drift인지 runtime flow ownership drift인지 아직 분리되지 않았을 때

### test-contract / stale expectation

- primary 판단 기준
  - old API shape
  - old fixture seed
  - stale literal/comparer wording
  - obsolete authoring requirement
  - baseline drift
- secondary 판단 기준
  - runtime/core나 authoring 근처에서 발생해도 authoritative semantics가 이미 acceptable이면 primary는 stale lane이다.
- risk note가 필요한 경우
  - stale row가 실제 runtime regression 후보와 같은 타입 주변에 있어 잘못 rollback을 유도할 위험이 있을 때
- 구현 lane으로 올리기 전 추가 evidence가 필요한 경우
  - stale-only인지 active regression인지 문서와 row provenance만으로 아직 확정되지 않았을 때

## 5. 우선순위 산정 규칙

### High priority 조건

- `5-signal worksheet` 총점이 `6+`이고, `multi-surface pressure` 또는 `blast radius`가 `2`다.
- 또는 총점 `5`라도 `touched cluster = 2`와 `contract revalidation = 2`가 동시에 있다.
- 또는 `primary lane`은 명확한데 같은 타입이 baseline freeze와 authoritative scenario pressure를 동시에 받는다.

### Medium priority 조건

- 총점 `3~5`다.
- 정적 구조 리스크는 분명하지만, 변경 압력이 한두 surface에만 걸린다.
- cut axis는 보이지만 `즉시 착수`로 잠글 만큼 evidence가 충분하지 않다.

### Low priority 조건

- 총점 `1~2`다.
- 정적 응집은 보이나 public seam과 authoritative behavior pressure가 약하다.
- 현재 중앙집중이 의도된 orchestration으로 기능하고 있고 재검증 범위도 좁다.

### evidence 부족 보류 조건

- 정적 집중은 보이지만 `5-signal worksheet` 근거가 약하다.
- lane ambiguity가 cross-lane mixed risk가 아니라 evidence 부족에서 온다.
- stale row나 contract drift가 있어도 runtime regression과 연결된 확증이 없다.

### 즉시 착수 / evidence 선행 / 보류 후보 분류 규칙

- `즉시 착수 후보`
  - `High`다.
  - `primary lane`, `유지 seam`, `first-cut axis`가 모두 고정됐다.
  - 추가 targeted evidence 없이 시작할 수 있다.
- `evidence 선행 후보`
  - `High` 또는 `Medium`이다.
  - targeted evidence 질문 하나를 풀어야 lane 또는 cut axis가 잠긴다.
- `보류 후보`
  - `Low` 또는 `Evidence-hold`다.
  - 또는 건강한 중앙집중이라 현 시점 절단 비용이 더 크다.
- 최종 문서에는 bucket만 적지 않는다.
- `왜 이 bucket인가`를 반드시 `priority tier + evidence status + seam clarity`로 함께 적는다.

## 6. 보완된 후속 plan

### Step 1. 평가 프레임 보정

- 목적
  - 기존 스펙을 유지하면서 `형식 충족`과 `실질 판단 품질`을 분리하는 규칙을 문서 상단에 고정한다.
- 다루는 lane
  - 전 lane 공통
- 핵심 확인 대상
  - 필수 섹션
  - 필수 타입
  - 핵심 결론 문장 규칙
  - Gate 기준
- 건드리면 안 되는 구조
  - 기존 영역 결론
  - public seam 유지 원칙
  - stale rollback 금지 원칙
- 기대되는 산출물
  - `Format Gate / Judgment Gate / Execution Gate`
  - 핵심 결론 문장 규칙
- exit condition
  - 형식상 통과 문서와 실제 실행 가능한 문서를 구분하는 체크리스트가 완성된다.
- 다음 step으로 넘어가기 위한 최소 evidence
  - 기존 스펙
  - baseline docs

### Step 2. 우선순위 워크시트 고정

- 목적
  - 정성적 표현만 남지 않도록 `5-signal worksheet`와 tier 규칙을 고정한다.
- 다루는 lane
  - 전 lane 공통
- 핵심 확인 대상
  - touched cluster
  - stale ledger row
  - multi-surface pressure
  - blast radius
  - contract revalidation
- 건드리면 안 되는 구조
  - 점수만으로 구조 실패를 선언하는 방식
  - 큰 파일=`High priority` 공식
- 기대되는 산출물
  - `High / Medium / Low / Evidence-hold` 규칙
  - candidate bucket 연결표
- exit condition
  - 모든 우선순위 문장이 worksheet 신호에 매핑 가능해진다.
- 다음 step으로 넘어가기 위한 최소 evidence
  - existing touched cluster docs
  - stale ledger
  - UI/core baseline docs

### Step 3. 예외적 targeted evidence 규칙 잠금

- 목적
  - 기본값은 기존 evidence만 쓰되, 경계선 타입에 한해 좁은 질문을 푸는 targeted evidence 예외를 허용한다.
- 다루는 lane
  - `runtime/core`
  - `composition/installer`
  - `authoring/prefab/view-factory`
- 핵심 확인 대상
  - 예외 후보 타입
  - 허용 질문 범위
  - lane 혼합 방지 기준
- 건드리면 안 되는 구조
  - refactor 정당화용 evidence 수집
  - runtime rollback 유도
  - multi-lane broad evidence sweep
- 기대되는 산출물
  - `default evidence`
  - `exception candidate`
  - `allowed targeted evidence`
  - `guardrail`
- exit condition
  - 경계선 타입에 대해 “언제 새 evidence를 모을 수 있는지”가 한 줄 규칙으로 설명 가능해진다.
- 다음 step으로 넘어가기 위한 최소 evidence
  - 예외 후보 타입 목록
  - 기존 baseline anchors

### Step 4. 타입별 판정 보강

- 목적
  - 9개 핵심 타입에 대해 기존 판정을 유지하면서 cut axis와 conservative hold 조건을 더 명확히 한다.
- 다루는 lane
  - 타입별 primary lane 중심
- 핵심 확인 대상
  - 유지 판정
  - 보강 판정 기준
  - evidence 부족 시 보수 유지
  - 예외 evidence 조건
- 건드리면 안 되는 구조
  - 타입별 재평가를 핑계로 기존 결론 뒤집기
  - public seam 재정의
- 기대되는 산출물
  - 9개 타입별 판정 규칙 세트
- exit condition
  - 각 타입이 `왜 지금 자르거나 보류하는지`를 priority tier와 함께 설명할 수 있다.
- 다음 step으로 넘어가기 위한 최소 evidence
  - 해당 타입의 기존 구조 guard
  - contract test
  - baseline evidence anchor

### Step 5. lane 분류와 risk note 잠금

- 목적
  - `primary / secondary / risk note` 체계를 도입해 mixed risk와 evidence 부족을 구분한다.
- 다루는 lane
  - 전 lane
- 핵심 확인 대상
  - lane 분류 기준
  - secondary lane 부여 기준
  - risk note 사용 기준
  - hold와 mixed risk 구분
- 건드리면 안 되는 구조
  - composition 문제를 `runtime/core` lane에 섞는 것
  - authoring contract pressure를 gameplay semantics pressure로 오인하는 것
- 기대되는 산출물
  - lane별 판단 기준표
  - risk note 사용 규칙
- exit condition
  - 모든 핵심 타입이 `primary lane`, 필요 시 `secondary lane`, 필요 시 `risk note`로 표현 가능해진다.
- 다음 step으로 넘어가기 위한 최소 evidence
  - 타입별 기존 판정
  - lane 성격 문서

### Step 6. 실행 backlog 연결

- 목적
  - 최종 평가 문서를 실제 구현 순서 잠금으로 이어지게 만든다.
- 다루는 lane
  - 전 lane
  - 실행 순서는 `runtime/core -> composition/installer -> authoring/prefab/view-factory -> test-contract / stale expectation`
- 핵심 확인 대상
  - `즉시 착수 후보`
  - `evidence 선행 후보`
  - `보류 후보`
  - 다음 PR의 first-cut axis
- 건드리면 안 되는 구조
  - lane 애매성을 이유로 구조 수정부터 시작하는 것
  - stale-contract 해결을 runtime 수정으로 푸는 것
- 기대되는 산출물
  - final bucket list
  - first-cut backlog 잠금 규칙
- exit condition
  - 첫 구현 PR이 어느 lane에서 어떤 seam을 보존하며 시작해야 하는지 결정 완료
- 다음 step으로 넘어가기 위한 최소 evidence
  - `priority tier`
  - `evidence status`
  - `seam clarity`
  - 위 세 항목이 각 후보에 기입돼 있어야 한다.

## Assumptions And Defaults

- 최종 산출물은 한국어 문서로 유지한다.
- 본문은 단계형 priority를 사용하고, `5-signal worksheet`는 본문 또는 별도 부록 형태로 남긴다.
- 새 targeted evidence는 예외 규칙이 걸린 타입에만 허용하고, 한 번에 한 질문만 푼다.
- `AudioManager`는 기본값 Low로 유지하고, contract leakage가 실제로 보일 때만 재분류한다.
- `TickPipeline`과 `GameplayInputHost`는 기존 결론상 가장 먼저 실무 리스크가 커질 가능성이 높지만, 여전히 evidence-driven priority 규칙을 통과해야만 `즉시 착수 후보`로 올린다.
