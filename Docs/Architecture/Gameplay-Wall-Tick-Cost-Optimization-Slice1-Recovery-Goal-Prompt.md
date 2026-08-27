# Gameplay Wall Tick Cost Optimization — Slice 1 Recovery Goal Prompt

> 실행 결과(2026-08-27): 이 prompt의 모든 phase가 완료됐다. 현재 판정과 exact test/performance evidence는 [Goal Plan의 recovery closeout](./Gameplay-Wall-Tick-Cost-Optimization-Slice1-Goal-Plan.md#2026-08-27--recovery-campaign-complete-goal-complete) 및 [Post-Closeout Audit §6](./Gameplay-Wall-Tick-Cost-Optimization-Slice1-Post-Closeout-Audit-2026-08-27.md#6-2026-08-27-recovery-closeout)에 기록돼 있다. 아래 `Current truth`와 단계 지시는 실행 시작 시점의 provenance다.

## 실행 지시

Gameplay Wall Tick Cost Optimization Slice 1의 재개방된 acceptance를 복구한다. 모든 구조·의미·focused/core/replay·성능 gate가 닫히기 전에는 `Goal complete` 또는 final retain을 선언하지 않는다.

안전한 다음 작업이 남아 있는 동안 계속 진행한다. 실제 권한 부족, 반복 재현되는 infrastructure blocker, 또는 해결할 수 없는 StrongContract 충돌이 있을 때만 중단하고, 그 전에는 같은 범위의 safe 대안을 소진한다.

## 시작 전 필수 읽기

1. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice1-Post-Closeout-Audit-2026-08-27.md`
2. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice1-Goal-Plan.md`
3. `Docs/Architecture/README.md`
4. `Docs/Testing/Gameplay-Test-Automation-Guide.md`
5. `AI_GIT_COMMIT_RULES.md`
6. `AGENTS.md`

`gameplay-contract-hardening`을 적용한다. 시작 시 `git status --short --branch`와 `git diff --stat`을 확인한다.

Recovery 준비 변경 allowlist는 다음 네 파일이다.

- `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice1-Goal-Plan.md`
- `Docs/Architecture/README.md`
- `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice1-Post-Closeout-Audit-2026-08-27.md`
- `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice1-Recovery-Goal-Prompt.md`

실행 시 status를 다시 inventory한다. 위 allowlist 밖의 dirty/untracked 파일은 user-owned로 취급해 수정·stage·revert하지 않는다. 새 J2M worktree가 필요하면 `j2m-worktree-add`만 사용한다. commit/push는 별도의 명시적 승인 범위에서만 수행한다.

## Current truth

- Goal: `reopened`
- runtime: `provisional retain candidate`
- 구조 목표: 충족
- inspected semantic paths: 차단 결함 미발견
- focused 기능 증거: pending
- A/B1/B2/C2/final formal performance acceptance: pending
- 기존 A/B1/B2 pair: historical supporting evidence only
- 기존 C2 `-13.028616%`와 `Goal complete`: invalid/superseded
- broad unfiltered `full`: 미실행
- GC/allocation: 미검증

## 비목표와 금지 사항

- Wall entity, Solid occupancy, `WorldState`/`WorldSnapshot`, `SurfaceCell`, Cleanup 의미를 변경하지 않는다.
- pair-matched protocol을 기본 15-run 대신 사용하지 않는다. 필요하면 측정 전에 사용자 명시 승인과 문서 amendment를 먼저 받는다.
- historical exact runtime SHA에 current runner/Player patch를 섞지 않는다.
- admitted official run을 p95 확인 후 제외하거나 warm-up으로 재분류하지 않는다.
- `project-wide green`, `full regression closed`, 근거 없는 speedup을 주장하지 않는다.

## Phase 1 — Admission hardening

Tests-first로 historical exact revision용 외부 validator와 regression fixture를 구현한다. 입력은 metrics path, manifest path, planned runtime revision, expected clean diff hash, immutable campaign identity다. 다음을 구조적으로 검증한다.

- `requestedResolution == actualResolution == [1920, 1080]`
- `render-idle`, `gameplay-neutral-tick` 정확히 두 phase
- 두 phase의 `sampleCount`, `validCpuMainSamples`, `validGpuSamples == 1200`
- gameplay의 `attemptedTicks`, `executedTicks`, `tickWallMilliseconds.count == 1200`
- Tick p95가 finite positive
- schema, `developmentBuild`, `warmupFrames`, sample frames, Tick interval, vSync, target frame rate
- manifest `HEAD == metrics revision == planned revision`
- clean diff hash
- Unity, OS, CPU, GPU, graphics API, quality, backend campaign identity

p95 집계 전에 `ADMITTED` 또는 구체적인 `REJECTED_*` verdict를 machine-readable하게 기록한다. record에는 다음을 포함한다.

- campaign ID, block, slot, state
- planned runtime SHA
- warm-up/official kind와 attempt number
- verdict와 reason
- metrics/manifest path
- validator SHA-256
- campaign-plan SHA-256

current HEAD의 Player는 actual resolution 일치를 확인한 뒤 warm-up을 시작하고 timeout이면 실패하도록 보강한다. runner도 phase별 JSON 검증을 수행하게 한다.

historical campaign은 raw SHA와 clean diff를 유지하고 외부 validator를 authoritative admission으로 사용한다. historical state에도 in-player wait가 필요하면 derived measurement commit, runtime tree hash, harness hash 방식의 별도 amendment 없이는 진행하지 않는다.

## Phase 2 — Focused evidence closure

1. 실제 `SnapshotEntityLogicProvider.Build`의 unknown/default branch가 full registration array를 사용하는지 직접 검증한다.
2. legacy/full-scan과 optimized provider의 observable accepted logic set 및 phase owner parity를 검증한다.
3. glide resolver의 첫 후보 실패 후 다음 후보 성공과 first-success 순서를 검증한다.
4. `TickPipeline.RunTick` diagnostics 실행 테스트를 Integration으로 이동하거나 pure accounting/runtime parity로 분리한다.
5. A-only revision `2dc8e5c53`에서 `TickReplayDeterminismTests`를 실행한다. 불가능하면 A replay 유예 amendment를 명시 승인받기 전까지 pending으로 둔다.

A-only replay가 통과하거나 사용자가 명시적으로 승인한 amendment가 기록되기 전에는 campaign freeze 및 fixed campaign으로 진행하지 않는다.

production runtime 결함이 발견되면 먼저 수정하고 다섯 performance state revision을 다시 pin한다. test-only 변경이면 Player runtime tree 동일성을 기록한다.

Goal Plan §10의 Stop/Rollback Gate는 전부 계속 authoritative하다. ownership/backing storage 노출, previous-result/general-copy/EventLog/hash/FullCanonical/replay/order, unscoped/unknown/glide/static/first-owner, diagnostics capture-off allocation, candidate-cache lifetime, core/touched-cluster 중 하나라도 깨지면 해당 package 작업을 중단하고 사용자 변경을 보존한 채 직전 green checkpoint로 되돌린다. 원인을 닫기 전 performance campaign으로 진행하지 않는다. B2 또는 C2를 reject하면서 원 objective를 유지하면 명시적 scope amendment나 재설계 없이 Goal complete로 처리하지 않는다.

## Phase 3 — Validation

변경된 최종 test revision에서 새로 추가하거나 이동한 Core·Infrastructure·Integration fixture 전체의 focused filter 목록을 먼저 고정하고 다음 순서로 실행한다.

1. `./run_tests.sh full --filter '<focused fixture list>'`
2. `./run_tests.sh core`
3. `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests`

Aggregate matching test가 0이면 실패로 처리한다. lane별 exact pass/fail counts를 기록한다. broad unfiltered `full`을 실행하지 않으면 정확한 이유를 남기고 touched-cluster 결과와 기존 broad baseline 상태를 분리한다.

`TickWorkDiagnosticsTests`, TickResult ownership, SnapshotEntityLogicProvider focused fixture와 Goal Plan §8.2의 six reflection PlayMode fixture를 실행한다. 다음 구조 counter도 다시 기록한다.

- Builder enumeration: `1`
- defensive copy: `0`
- owned wrapper: `1`
- trusted share: `1`
- 기본 Wall/`None` probe: `0`

## Phase 4 — Campaign freeze and storage

Runtime revision을 다음으로 pin한다.

- baseline: `5d338c54a890bb5225ddda9d769f880846b8f1ca`
- A: `2dc8e5c53c743fbc531c6ed7421a91baa3349a12`
- B1: `bfe16e8ddd60af03ad036e163fc917cba85f698e`
  - production runtime은 `79889c4b7`과 동일
- B2: `4498148adc19a9f1fea02b83732756e2c9e6e05e`
- C2: `29d26ab18b0023a2a7815786f71dfbd2efd5c083`

`/mnt/d/J2M/worktrees` 아래 정책 준수 worktree와 worktree 전용 private `Library`를 사용한다. `j2m-worktree-audit` PASS와 clean diff를 확인한다. 새 worktree가 필요하면 D free space가 30 GiB 이상인지 먼저 확인한다. evidence와 build는 각각 `/mnt/d/J2M/evidence`, `/mnt/d/J2M/builds` 아래에 둔다.

warm-up/official slot, revisions, settings, admission/retry/aggregation policy를 immutable `campaign-plan.json`으로 고정하고 SHA-256을 기록한다. validator content SHA-256도 고정한다. validator나 plan 중 하나라도 바뀌면 기존 campaign을 이어가지 않고 새 campaign ID로 처음부터 시작한다.

## Phase 5 — Fixed 5-state/15-run campaign

각 state당 admitted full-run warm-up 하나를 확보하고 항상 폐기한다. warm-up admission 실패는 같은 warm-up slot에서 retry하고 모든 attempt를 기록한다. 권장 warm-up 순서는 다음과 같다.

```text
C2 -> B2 -> B1 -> A -> baseline
```

Official block은 다음 순서로 고정한다.

```text
Block 1: baseline -> A -> B1 -> B2 -> C2
Block 2: C2 -> B2 -> B1 -> A -> baseline
Block 3: B1 -> C2 -> A -> baseline -> B2
```

실패는 같은 slot에서 attempt number를 증가시켜 retry하고 evidence를 보존한다. campaign index가 15개 official slot마다 admitted attempt를 정확히 하나씩 가지는지 자동 검증한다.

## Phase 6 — Aggregation and decision

원 JSON double 값으로 state별 정확히 세 p95의 median을 계산한다. pass/fail 판정을 먼저 수행하고 표시할 때만 소수점 여섯 자리로 반올림한다.

다음 다섯 delta가 모두 `<= +5%`여야 한다.

- baseline -> A
- A -> B1
- B1 -> B2
- B2 -> C2
- baseline -> C2

원값, median, min/max range, 실행 순서, rejected/retry 목록, 보조 paired delta를 기록한다. 범위가 겹치면 speedup을 주장하지 않는다. GC가 unavailable이면 allocation은 미검증으로 유지한다.

실패 시 다음과 같이 처리한다.

- A 실패: diagnostics hook을 rollback하거나 재설계하고 downstream state와 campaign을 다시 고정한다.
- B1 실패: B1을 rollback하거나 재설계하고 B2/C2 state와 campaign을 다시 고정한다.
- B2 실패: B2를 rollback하거나 재설계한다. copy `0` objective를 유지하면 B2 없이 완료할 수 없다.
- C2 실패: C2를 rollback하거나 재설계한다. Wall probe `0` objective를 유지하면 C2 없이 완료할 수 없다.
- final만 실패: 누적 회귀 attribution을 다시 수행한다.

## 완료 조건과 진행 기록

모든 구조·의미·focused/core/replay와 다섯 성능 gate가 통과할 때만 dated closeout을 추가하고 Goal complete를 재판정한다. 기존 invalid evidence는 삭제하지 않는다.

각 phase 종료 시 Goal Plan/Post-Closeout Audit의 dated progress block에 다음을 갱신한다.

- Goal objective
- Current package
- revision/branch
- pre-existing changes preserved
- tests-first assertion red/green과 exact pass/fail counts
- 구조 counter before/after
- 실행 명령과 evidence path
- admission/retry 및 raw p95/median/range
- open risks
- tests not run/reason
- next safe action
- provisional/retain/reject 판정
