# Post-Stage-Content Bounded Lane Operations

이 문서는 stage-content close 이후 follow-up bounded lane 운영 truth-source다. stage-content canonical runtime path는 reopen하지 않고, lane별 backlog triage, handoff, close claim, execution order를 고정한다.

## Preserved Strengths

- stage-content lane은 close 상태로 유지한다.
- canonical runtime path `StageId -> StageCatalogResolver -> StageContentEntry -> GameplayDefinition/PresentationDefinition`은 no-touch다.
- `StageRuntimeBuilder` / `StageRuntimeBuildResult` gameplay-only 경계는 어떤 follow-up lane에서도 흔들지 않는다.
- broad backlog는 stage-content canonical path 변경으로 회복하지 않는다.
- editor direct-play adoption은 운영 정착 lane이며 structure rollback lane이 아니다.
- support tree relocation은 decision record를 먼저 닫고 후행 판단으로 남긴다.
- audio/BGM과 terrain/occupancy는 stage-content와 분리된 전문 lane이다.

## Lane A Recovery Streams

### A1. new red candidate isolation

- first-oracle:
  - 이전 pinned full artifact에 없었고 최신 rerun에서 새로 생긴 fail
  - direct touched cluster 변경 후 같은 revision에서 처음 관찰된 fail
- purpose:
  - 신규 오염을 기존 baseline red와 즉시 분리한다.
- exit gate:
  - open row `0`
  - unrelated contamination carryover `0`

### A2. host/view/bootstrap adjacency

- first-oracle:
  - host root, bootstrap root, installer, presenter, prefab/view, scene composition, stage-backed scene UX/host glue의 첫 오라클이 틀린 경우
- scope:
  - `GameplaySceneHost`
  - `GameplayUiFlowInstaller`
  - stage-backed bootstrap/view 경계
  - launcher smoke 인접 host/view 이슈
- exit gate:
  - open row `0`
  - stage-content canonical path touch `0`

### A3. direct unrelated backlog

- first-oracle:
  - stage-content와 무관하고 host/bootstrap 인접도 아닌 gameplay/UI/infra 영역의 기능 또는 test oracle이 먼저 틀린 경우
- scope:
  - direct non-stage-content backlog 전반
- exit gate:
  - open row `0`

### A4. baseline red continuation triage

- first-oracle:
  - 최신 rerun에서도 살아 있는 pre-existing red인데 `A1/A2/A3`로 바로 떨어지지 않는 carryover row
- purpose:
  - historical red를 live ledger로 재동결하고 owner/handoff를 잠근다.
- exit gate:
  - live continuation row `0`

## Lane A Priority And Recovery Gate

- execution priority는 `A1 -> A2 -> A3 -> A4`로 고정한다.
- 이유:
  - `A1`을 먼저 닫아야 현재 change contamination과 기존 baseline을 섞지 않는다.
  - `A2`를 빨리 정리해야 host/view 오해가 stage-content canonical path touch로 번지지 않는다.
  - `A3`는 direct unrelated recovery 본체다.
  - `A4`는 continuation ledger 정리이므로 마지막에 닫는다.
- `full-lane baseline recovered`는 아래 조건이 모두 참일 때만 사용한다.
  - `A1` open row `0`
  - `A2` open row `0`
  - `A3` open row `0`
  - `A4` live continuation row `0`
  - latest same-revision `./run_tests.sh full` green
  - cross-lane blocking handoff queue `0`

## Lane A Exclusion And Handoff Codebook

- Lane A에서 처리하지 않는 canonical-path row:
  - `StageCatalogResolver`
  - `StageRuntimeContentResolver`
  - `StageContentEntry`
  - `defaultStageId`
  - `ResolveLegacy`
  - direct `StageDefinition` production reference
  - `scene.*residue`
  - direct-play catalog coverage blocker
- handoff destination:
  - direct-play launcher, catalog coverage, plain Play unsupported, onboarding/menu mismatch -> `Lane B`
  - support tree 위치, relocation 필요성, consumed asset complete 여부 -> `Lane C`
  - persistent BGM owner, registry, same-root audio flow, cross-scene continuity -> `Lane D`
  - occupancy claim, terrain query, legality, reservation, modifier, traversal/settlement semantics -> `Lane E`
  - wording drift, close note, doc test, CI claim, artifact scope 혼합 -> `Lane F`

## Handoff Row Schema

- `source lane`
- `target lane`
- `reason`
- `blocking claim`
- `required evidence`

모든 handoff row는 위 다섯 필드를 채운다. 같은 row는 동시에 두 lane에서 active 처리하지 않는다.

## Lane Bounded Execution Order

1. `Lane F` wording/evidence lock
2. `Lane B` soft adoption
3. `Lane A1` new red candidate isolation
4. `Lane A2` host/view/bootstrap adjacency
5. `Lane C` deferred decision record
6. `Lane D` decision closed
7. `Lane E` decision closed
8. `Lane B` hard enforcement
9. `Lane A3` direct unrelated backlog
10. `Lane A4` baseline red continuation triage
11. `Lane D2` gate review
12. `Lane E2` gate review
13. `full-lane baseline recovered` eligibility review

## Stage Execution Sequence

### Step 1. Governance Lock

- lock:
  - wording table
  - close template
  - artifact-date rule
  - handoff ledger schema

### Step 2. Direct-Play Soft Adoption

- align:
  - onboarding
  - menu path
  - smoke checklist
  - warning text

### Step 3. Full Baseline Re-Freeze

- inputs:
  - current full XML
  - per-class histogram
  - previous pinned artifact
  - handoff codebook

### Step 4. Lane C/D/E Decision Close

- close:
  - support tree deferred decision record
  - BGM ownership gate ADR
  - terrain/occupancy gate ADR

### Step 5. Direct-Play Hard Enforcement And A1/A2 Recovery

- enforce:
  - launcher bypass is unsupported misuse
- recover:
  - `A1`
  - `A2`

### Step 6. A3/A4 Recovery

- recover:
  - direct unrelated backlog
  - baseline continuation

### Step 7. D/E Gate Review

- allow implementation only after gate green

### Step 8. Broad Recovery Claim Review

- review `full-lane baseline recovered`
- do not claim `broad project-wide green` without the required same-window companion evidence

## Risks

- canonical-path row가 Lane A에 섞이면 stage-content reopen risk가 생긴다.
- direct-play friction을 구조 rollback으로 풀려는 압력이 다시 생길 수 있다.
- `deferred` decision record가 trigger 없이 남으면 indefinite hold로 흐른다.
- D/E가 decision closed만으로 implementation start로 오인될 수 있다.
- governance lane이 functional backlog를 가리는 방향으로 오용될 수 있다.

## Required Guards

- lane 시작 전에 handoff codebook을 먼저 본다.
- close note마다 `open functional backlog / handoff`를 남긴다.
- mixed-date artifact는 같은 validation claim 근거로 사용하지 않는다.
