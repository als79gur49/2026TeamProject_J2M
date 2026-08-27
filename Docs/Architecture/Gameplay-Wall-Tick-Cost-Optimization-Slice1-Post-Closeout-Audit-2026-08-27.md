# Gameplay Wall Tick Cost Optimization — Slice 1 Post-Closeout Audit and Recovery Plan

- 상태: Recovery complete — corrected focused/core/replay 및 고정 5-state/15-run acceptance 통과, Goal complete
- 작성일: 2026-08-27
- 감사 대상 runtime revision: `29d26ab18b0023a2a7815786f71dfbd2efd5c083`
- 원 실행 문서: [Gameplay Wall Tick Cost Optimization — Slice 1 Goal Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice1-Goal-Plan.md)
- 적용 가드레일: `gameplay-contract-hardening`

## 1. 감사 결론

요구사항, 성능 evidence, production 계약/테스트를 세 서브 에이전트가 독립적으로 재검토하고 상호 반론 검토했다. 현재 판정은 다음으로 고정한다.

| 영역 | 현재 판정 | 근거 |
|---|---|---|
| production runtime | provisional retain candidate | 감사한 경로에서 authoritative Wall/Solid occupancy, `FinalEntities` ownership/lifetime, entity-major/Factory order를 깨는 차단 결함은 발견되지 않음 |
| 구조 목표 | pass | Builder defensive copy `0`, owned wrapper `1`, 기본 Wall/`None` probe `0`이 코드와 구조 계측에 남아 있음 |
| StrongContract 구현 | inspected paths에서 위반 미발견 | general-copy, previous-result immutability, static-first/first-owner, unscoped Factory 경로는 유지됨 |
| focused evidence | conditional | unknown hot fallback, legacy accepted-owner parity, glide first-success ordering의 직접 증거가 부족하고 diagnostics 실행 테스트 2개가 Unit에 있음 |
| A/B1 성능 | historical supporting only | 개별 pair admission과 산술은 유효하지만 새 formal acceptance가 요구하는 하나의 15-run cohort가 아님 |
| redesigned B2 성능 | provisional | pair-specific local control 기준 산술은 pass지만 원래 고정 15-run cohort가 아님 |
| C2 성능 | invalid / 판정 불가 | official triplet 3회 중 2회의 실제 해상도가 `1080x1080` |
| Slice 1 Goal | reopened | retained package의 성능 gate가 유효하게 닫히지 않았으므로 기존 `Goal complete` 선언을 철회함 |

현재 증거는 C2 pass도 reject도 확정하지 못한다. 따라서 runtime을 즉시 rollback하지는 않지만, corrected evidence 전에는 final retain, performance non-regression 또는 speedup을 주장하지 않는다. broad unfiltered `full`은 실행되지 않았으므로 project-wide green도 주장하지 않는다.

## 2. 완료 판정을 철회한 이유

### 2.1 C2 official triplet의 실제 해상도 불일치

원 계획은 모든 상태를 동일 PC, 해상도, 품질, 표본 수에서 측정하도록 요구한다. 실제 C2 official triplet은 다음과 같다.

| Run | Requested | Actual | Tick p95 | 재분류 |
|---|---:|---:|---:|---|
| `20260826T200124Z` | `1920x1080` | `1920x1080` | `7.123190 ms` | historical diagnostic |
| `20260826T200536Z` | `1920x1080` | **`1080x1080`** | `5.678465 ms` | rejected resolution |
| `20260826T200711Z` | `1920x1080` | **`1080x1080`** | `5.715655 ms` | rejected resolution |

두 invalid run이 기존 C2 중앙값 `5.715655 ms`를 결정했다. 따라서 기존 `-13.028616%`와 5% non-regression pass는 산술 오류가 아니라 admission 오류로 무효다. 해상도가 맞는 official C2 run은 하나뿐이므로 그 값이 B2 control 중앙값보다 높더라도 reject 표본으로 사용할 수 없다.

### 2.2 고정 5-state cohort에서 pair-specific control로 변경됨

원 계획은 baseline/A/B1/B2/C2의 다섯 상태를 각각 3회 수집한 하나의 15-run cohort로 모든 adjacent/final 판정을 계산하도록 고정한다. 기존 closeout은 시간 근접성을 높이기 위해 pair마다 A, B1, B2 control triplet을 다시 수집했다. 이 방식은 noisy host에서 합리적일 수 있지만 승인되지 않은 protocol amendment다.

하나의 기록된 triplet만 상태별로 선택한 strict chain에서는 다음 결과가 나온다.

| 상태 | 기록된 중앙값 |
|---|---:|
| baseline | `7.348720 ms` |
| A | `7.188575 ms` |
| B1 | `7.251195 ms` |
| redesigned B2 | `7.877540 ms` |
| C2 | `5.715655 ms` — 해상도 불일치로 무효 |

이 chain의 B1 -> B2는 `+8.637818%`로 5% gate를 넘는다. 반면 기존 closeout의 local B1 -> B2는 `-1.766082%`다. 어느 결과가 authoritative인지 원 문서가 바뀌지 않은 채 control triplet만 교체됐으므로, redesigned B2도 새 고정 cohort 전에는 provisional이다.

### 2.3 runner admission 결함과 사후 제외 위험

현재 `run_tests.sh`는 metrics JSON을 구조적으로 검사하지 않고 파일 전체에서 `"validGpuSamples":1200` 문자열이 한 번 존재하는지만 확인한다. 이 때문에 idle `1200`, gameplay `1199` 또는 `1197`인 run은 통과하고, 두 phase가 모두 `1199`인 run은 실패한다. `requestedResolution`과 `actualResolution` 일치도 검사하지 않는다.

또한 원 계획에 없던 full measurement warm-up run이 결과 집합에서 제외됐고, C2 후보는 시간 순으로 `8.202650 -> 7.123190 -> 6.012690 -> 5.678465 -> 5.715655 ms`로 움직였다. 기존 값을 새 official set에 재사용하지 않고, warm-up 및 retry 규칙을 결과 확인 전에 고정해야 한다.

### 2.4 계속 유효한 evidence

- 기존 median/range/delta 산술은 재검산 결과 정확하다.
- 최초 B2 `+24.196145%`, 최초 C `+22.643586%`, 첫 concrete C `+6.416429%` rejection은 당시 동일 조건 alternating pair의 historical rollback evidence로 유지한다.
- redesigned B2 local alternating set은 supporting evidence로 유지하되 strict fixed-chain acceptance를 대신하지 않는다.
- 모든 기록된 official run의 Tick `attempted/executed/count`는 `1200/1200/1200`이다.
- 모든 검토 run에서 GC counter는 unavailable이므로 sustained allocation 비악화는 계속 미검증이다.
- raw Tick 1,200개 표본은 evidence에 저장되지 않아 각 JSON의 p95 자체를 원표본으로 독립 재계산할 수 없다.

기존 evidence 파일은 삭제하지 않는다. invalid C2 triplet, runner-failed run, 사후 warm-up은 historical diagnostic으로 보존하고 새 formal acceptance에 포함하지 않는다.

## 3. 확정한 기본 복구 경로

기본 권고는 **고정 5상태 x 3회, 총 15개 official run의 완전 재측정**이다. B2/C2만 다시 재는 최소안은 원 요구사항을 닫지 못하므로 기본안으로 사용하지 않는다.

### Gate 0 — 현재 판정 동결

- 현재 상태를 `Goal reopened`, `runtime provisional retain candidate`, `performance pending`으로 유지한다.
- A/B1/B2/C2 및 final formal performance acceptance를 모두 pending으로 둔다. 기존 A/B1/B2 pair 결과는 historical supporting evidence로만 유지한다.
- 기존 C2 `-13.028616%`, C2 pass, final/baseline pass를 formal acceptance에서 사용하지 않는다.
- corrected evidence 전에 후속 Cleanup/trace 최적화 Slice로 진입하지 않는다.

### Gate 1 — measurement admission을 먼저 수정

모든 historical exact-revision capture에는 동일한 **외부 post-capture validator**를 적용하고, p95 집계 전에 다음을 자동 확인해야 한다.

- `requestedResolution == actualResolution == [1920, 1080]`.
- manifest `HEAD == metrics.revision == planned revision`.
- worktree diff hash가 clean-worktree hash와 일치한다.
- Unity version, OS, CPU, GPU, graphics API, quality, backend, vSync, target frame rate가 모든 official run에서 같다.
- phase는 `render-idle`, `gameplay-neutral-tick` 정확히 두 개다.
- 두 phase 각각 `sampleCount`, `validCpuMainSamples`, `validGpuSamples`가 모두 `1200`이다.
- gameplay phase의 `attemptedTicks`, `executedTicks`, `tickWallMilliseconds.count`가 모두 `1200`이고 p95가 finite positive다.
- admission 결과를 `ADMITTED`, `REJECTED_IDENTITY`, `REJECTED_RESOLUTION`, `REJECTED_SAMPLE_COUNT`, `REJECTED_REVISION`, `REJECTED_RUNTIME` 중 하나로 p95 집계 전에 기록한다.
- machine-readable admission record에는 campaign ID, block, slot, state, planned runtime SHA, warm-up/official kind, attempt number, verdict/reason, metrics/manifest path, validator SHA-256, campaign-plan SHA-256을 저장한다.

GPU는 현재 wrapper가 CPU/GPU completeness를 요구한다고 문서화돼 있으므로 두 phase 각각 exact `1200`을 기본 정책으로 유지한다. Tick-only gate에서 GPU를 제외하려면 결과 수집 전에 별도 protocol amendment를 승인하고 GPU 비악화 주장을 포기해야 한다. 기존 결과를 살리기 위한 사후 완화는 금지한다.

고정된 과거 runtime revision에는 새 Player probe patch를 적용하지 않는다. 그래야 manifest `HEAD == planned revision`과 clean diff를 동시에 지킬 수 있다. 실제 해상도가 다르면 외부 validator가 p95를 집계하지 않고 같은 state/slot을 자동 retry한다.

현재 HEAD의 automation hardening으로는 Player가 `Screen.width/height == requested`를 확인한 뒤 warm-up을 시작하고 timeout이면 실패하도록 수정하며, runner도 같은 JSON 구조 검증을 수행하게 한다. 이 변경과 regression fixture는 향후 capture의 기본 안전장치지만, historical exact-revision campaign에서는 동일 외부 validator가 authoritative admission이다. in-player wait를 historical 상태에도 강제하려면 각 runtime tree 위에 동일 measurement-harness commit을 만들고 runtime tree hash와 harness hash를 분리 기록하는 별도 protocol amendment가 필요하다.

### Gate 2 — 저비용 계약/검증 공백을 expensive campaign 전에 닫음

다음 항목은 성능 수치를 직접 바꾸지는 않지만, 새 측정 뒤 production 결함이 발견되어 campaign을 다시 하는 일을 피하기 위해 먼저 처리한다.

1. 실제 `SnapshotEntityLogicProvider.Build`의 unknown/default branch가 `_allEntityLogicFactories`를 선택함을 직접 검증한다. 사용되지 않는 private resolver reflection만으로 대체하지 않는다.
2. legacy/full-scan과 optimized provider가 observable accepted logic set 및 phase owner를 동일하게 만드는 fixture를 추가한다.
3. glide resolver의 첫 후보 실패 -> 다음 후보 성공과 first-success 등록 순서를 직접 검증한다.
4. `TickPipeline.RunTick`을 호출하는 diagnostics 실행 테스트 2개를 Scenario/Integration으로 이동하거나 pure accounting과 runtime parity로 분리한다.
5. A-only revision `2dc8e5c53`에서 `TickReplayDeterminismTests` replay evidence를 보완하거나, A replay를 B1까지 유예한다는 amendment를 명시적으로 승인한다.
6. 변경된 최종 test revision에서 focused, `./run_tests.sh core`, replay를 다시 실행하고 exact counts를 기록한다.

이 단계에서 production runtime을 바꾸는 결함이 발견되면 먼저 수정하고 다섯 성능 상태 revision을 다시 고정한다. test-only 변경만 있으면 Player runtime tree가 동일함을 기록하고 아래 pinned runtime revisions를 유지할 수 있다.

### Gate 3 — official 5-state campaign

권장 runtime revision은 다음으로 고정한다.

| 상태 | Revision | 비고 |
|---|---|---|
| baseline | `5d338c54a890bb5225ddda9d769f880846b8f1ca` | 변경 전 |
| A | `2dc8e5c53c743fbc531c6ed7421a91baa3349a12` | diagnostics capture Off |
| B1 | `bfe16e8ddd60af03ad036e163fc917cba85f698e` | redesigned B2 직전 retained-B1 runtime; production runtime은 `79889c4b7`과 동일 |
| B2 | `4498148adc19a9f1fea02b83732756e2c9e6e05e` | redesigned ownership path |
| C2 | `29d26ab18b0023a2a7815786f71dfbd2efd5c083` | inline zero-candidate dispatch |

측정 작업공간은 `/mnt/d/J2M/worktrees` 아래 정책 준수 worktree와 그 worktree 전용 private `Library`를 사용한다. 시작 전 `j2m-worktree-audit` PASS와 clean diff를 확인한다. 새 worktree가 필요하면 `j2m-worktree-add`만 사용하고 D 여유 공간이 최소 30 GiB인지 먼저 확인한다. evidence/build는 각각 `/mnt/d/J2M/evidence`, `/mnt/d/J2M/builds` 아래에 둔다.

Player 내부 120-frame warm-up과 별도로 각 state당 **한 개의 admitted full measurement warm-up**을 확보하고 항상 폐기한다. warm-up admission 실패는 같은 warm-up slot에서 retry하며 모든 attempt와 실패 사유를 기록한다. admitted warm-up의 값에 따라 추가 warm-up으로 재분류하지 않는다. 권장 warm-up 순서는 `C2 -> B2 -> B1 -> A -> baseline`이다.

15개 official slot은 측정 전에 다음 순서로 고정한다.

```text
Block 1: baseline -> A -> B1 -> B2 -> C2
Block 2: C2 -> B2 -> B1 -> A -> baseline
Block 3: B1 -> C2 -> A -> baseline -> B2
```

warm-up/official slot, state revision, 실행 설정, admission 정책을 immutable `campaign-plan.json`으로 만들고 campaign 시작 전에 SHA-256을 기록한다. external validator 구현도 content SHA-256을 기록한다. campaign 도중 validator나 plan이 바뀌면 기존 campaign을 이어가지 않고 새 campaign ID로 처음부터 다시 시작한다.

각 상태는 정확히 3개의 admitted run을 가진다. admission 실패는 같은 state/slot에서 attempt number를 증가시켜 자동 retry하고 실패 evidence와 사유를 보존한다. admission을 통과한 official run은 p95를 본 뒤 제외하거나 warm-up으로 재분류하지 않는다. campaign index는 15개 official slot 각각에 admitted attempt가 정확히 하나인지 자동 검증해야 한다.

### Gate 4 — 하나의 cohort로만 판정

각 상태의 official 값은 같은 campaign에서 수집한 정확히 세 Tick p95의 중앙값이다.

```text
M0 = median(baseline official 3)
M1 = median(A official 3)
M2 = median(B1 official 3)
M3 = median(B2 official 3)
M4 = median(C2 official 3)
```

다음 다섯 delta가 모두 `<= +5%`여야 한다.

```text
baseline -> A  = (M1 / M0 - 1) * 100
A -> B1        = (M2 / M1 - 1) * 100
B1 -> B2       = (M3 / M2 - 1) * 100
B2 -> C2       = (M4 / M3 - 1) * 100
baseline -> C2 = (M4 / M0 - 1) * 100
```

계산은 원 JSON double 값으로 median과 delta 및 pass/fail을 먼저 구하고, 표시할 때만 소수점 여섯 자리로 반올림한다. 상태별 세 원값, median, min/max range, block/실행 순서, rejected attempt/retry 사유를 모두 기록한다. 인접 paired delta는 보조 지표로만 제시한다. 범위가 겹치면 speedup을 확정적으로 표현하지 않는다. GC가 계속 unavailable이면 allocation은 계속 미검증으로 남긴다.

### Gate 5 — 결과별 종료 조건

- 다섯 시간 gate와 구조·의미·focused/core/replay gate가 모두 통과하면 새 dated closeout으로 Goal을 다시 완료한다.
- A가 5%를 넘으면 diagnostics hook을 rollback하거나 재설계하고 A 이후 상태 revision 및 campaign을 다시 고정한다.
- B1이 5%를 넘으면 B1을 rollback하거나 재설계하고 그 위에 누적된 B2/C2 상태 revision 및 campaign을 다시 고정한다.
- B2가 5%를 넘으면 B2를 reject/rollback하거나 재설계한다. 원 objective의 copy `0`을 유지하면 B2 없이 Goal complete는 불가하다.
- C2가 5%를 넘으면 C2를 reject/rollback하거나 재설계한다. 원 objective의 기본 Wall probe `0`을 유지하면 C2 없이 Goal complete는 불가하다.
- final gate만 실패해도 완료하지 않고 누적 회귀 attribution을 다시 수행한다.
- production runtime이 바뀌면 변경된 revision에서 관련 구조/의미/성능 gate를 다시 실행한다.

## 4. 조건부 대안 — pair-matched protocol amendment

사용자가 원래 15-run 요구사항을 명시적으로 변경하는 경우에만 pair-matched 방식을 authoritative protocol로 채택할 수 있다.

- 각 adjacent pair는 독립 same-session control/candidate triplet을 사용한다.
- 동일 논리 상태도 pair마다 다시 측정하며 서로 다른 pair의 절대값을 합성하지 않는다.
- 네 adjacent pair만으로 최소 24 official runs가 필요하다.
- baseline/final도 same-session pair로 요구하면 총 30 official runs가 필요하다.
- 총 run 수, 순서, warm-up, admission, aggregation을 측정 전에 문서로 고정한다.

일정상 최소 복구만 승인할 경우 B2/C2를 `B2 -> C2`, `C2 -> B2`, `B2 -> C2`의 세 paired block으로 다시 측정할 수 있다. 그러나 이 6-run 안은 C2 local gate만 닫고 strict-chain B1 -> B2 `+8.637818%` 논점을 해결하지 못하므로 Slice 1 원 요구사항 전체 완료 증거가 아니다.

## 5. 현재 승인된 다음 safe action

1. Goal 재개방과 기존 C2 performance verdict 철회를 문서 current truth로 유지한다.
2. historical campaign용 외부 phase별 JSON admission을 고정하고, 현재 HEAD에는 actual-resolution wait/검사와 runner regression fixture를 별도 automation hardening으로 구현한다.
3. unknown/legacy-owner/glide 증거 공백과 diagnostics test stratification을 보완한 뒤 focused/core/replay를 실행한다.
4. 고정 5-state/15-run campaign을 수행한다.
5. 동일 cohort로 다섯 gate를 계산해 retain 또는 rollback/redesign을 결정한다.
6. 결과를 새 closeout section으로 남기기 전에는 후속 대형 최적화 Slice로 이동하지 않는다.

이 문서는 기존 구현 과정과 historical evidence를 삭제하지 않는다. 원 Goal Plan의 2026-08-27 완료 기록은 당시 판단의 provenance로 남기되, 현재 status와 performance/Goal-complete 판정은 이 감사 문서가 supersede한다.

## 6. 2026-08-27 recovery closeout

이 절은 §1~§5의 reopened/pending 상태를 supersede한다. 감사에서 요구한 admission hardening, focused proof closure, A-only replay 및 단일 fixed cohort 재측정을 모두 완료했다.

- Current package: S1-A, retained B1, redesigned B2, C2 모두 retain.
- Revision/branch: 주 작업트리 HEAD `e1b8238`, branch `codex/third-party-license-inventory`; runtime states는 §3 Gate 3의 다섯 exact SHA 그대로다. production gameplay runtime을 추가 변경하지 않았고 commit/push하지 않았다.
- Tests-first/validation: admission scaffold red `12 total / 10 failed / 2 error` -> validator/campaign suite `19/19`; durable focused EditMode `20 total / 20 passed / 0 failed / 0 skipped`; durable focused PlayMode `101 total / 97 passed / 0 failed / 4 skipped`; 교정 후 core EditMode `228 total / 228 passed / 0 failed / 0 skipped`; core PlayMode `111 total / 107 passed / 0 failed / 4 skipped`; current replay `59/59`; pinned A replay `59/59`.
- Structure: Builder enumeration `1`, defensive copy `0`, owned wrapper `1`, trusted share `1`, 기본 Wall/`None` probe `0`. actual Build unknown fallback, legacy accepted-owner parity, glide first-success와 diagnostics Scenario stratification 공백을 닫았다. 사후 재감사에서 발견한 새 legacy-owner/glide Core proof의 composition-root helper 사용은 raw `WorldSnapshot`으로 교정했다.
- Campaign: `slice1-recovery-20260827T084937Z`, plan SHA `f5bf38ca65deb29d5403d67e01d8c257be6bd0699edaee38ec6d38a0f51d5f3b`, validator SHA `0e9947a9780f49a2513684b175c2db50064408805e23e20d97bffa510b7ac813`. warm-up `5/5`, official `15/15`, index issues `0`, attempt `29`(`20` admitted, `9` rejected)이다.
- Median p95: baseline `6.152735`, A `6.238940`, B1 `6.074515`, B2 `6.057070`, C2 `6.039985 ms`.
- Fixed-cohort gates: baseline->A `+1.401084%`, A->B1 `-2.635464%`, B1->B2 `-0.287183%`, B2->C2 `-0.282067%`, baseline->C2 `-1.832518%`; 모두 `<= +5%`로 pass다.
- Evidence: `/mnt/d/J2M/evidence/gameplay-performance/slice1-recovery-20260827T084937Z/{campaign-plan.json,campaign-index.json,aggregate.json}`와 같은 root의 `captures/`, `admissions/`; build root `/mnt/d/J2M/builds/gameplay-performance/slice1-recovery-20260827T084937Z`. 재감사 close evidence는 `/mnt/d/J2M/evidence/slice1-recovery-closeout-20260827T113317Z/{focused-editmode,focused-playmode,core}`에 lane별로 분리 보존해 XML/log 덮어쓰기를 방지했다.
- Limits: raw 범위가 겹치고 A 표본 하나가 `9.177435 ms`이므로 speedup을 주장하지 않는다. GC/allocation은 `UNVERIFIED`; broad unfiltered `full`은 known red baseline과 scoped recovery 때문에 미실행; asset/manual editor validation 대상 변경은 없다.
- Verdict: original structural objective와 의미/replay 계약을 유지했고 formal time gates가 모두 닫혔다. runtime을 final retain하고 Slice 1 Goal을 `complete`로 재판정한다.
