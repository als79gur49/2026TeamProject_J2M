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

## Lane A Live Row Ledger

- 모든 open row는 same-revision full XML 기준 canonical test id로 import한다.
- minimum fields:
  - `row id/test name`
  - `status`
  - `classification date`
  - `source artifact`
  - `first wrong oracle`
  - `owner lane`
  - `current owner`
  - `blocking claim`
  - `required evidence`
  - `next action`
  - `last reviewed at`
  - `rationale summary`
- recommended fields:
  - `handoff target`
  - `classification confidence`
  - `failure shape summary`
  - `linked historical row`
  - `confirming artifact`

## Lane A Row Lifecycle And Same-Revision Refresh

- lifecycle:
  - `open -> triaged -> active -> fixed / handed-off / historical`
- `blocked`는 `triaged` 또는 `active` 상태에서 evidence 부족, handoff acceptance 대기, prerequisite 미충족일 때만 임시로 사용한다.
- same-revision evidence refresh rules:
  - 같은 test id + 같은 failure shape면 기존 row를 재사용한다.
  - 이 경우 `last reviewed at`, `required evidence`, `next action`, `classification confidence`만 갱신한다.
  - 같은 test id라도 failure shape가 materially changed면 기존 row를 `historical`로 내리고 새 row를 만든다.
  - 새 row는 기본적으로 `A1 provisional`에서 시작한다.
- owner change rules:
  - same-revision artifact에서 `first wrong oracle` 이동이 확인된 경우
  - accepted handoff가 기록된 경우
  - 기존 owner가 target lane reject reason과 함께 same-revision 반증을 낸 경우
- convenience re-routing, 사람 교체, 추측만으로 owner를 바꾸지 않는다.

## Lane A Classification Tie-Break And Confidence Rules

- tie-breaker order:
  1. `newness / failure-shape change`
  2. `first wrong oracle`
  3. `explicit lane exclusion / handoff codebook`
  4. `A2 host/view/bootstrap adjacency`
  5. `historical carryover`
- `first wrong oracle`과 `owner surface`가 다르면 `first wrong oracle`를 우선한다.
- `owner surface`는 `first wrong oracle`가 불명확할 때만 보조 기준으로 사용한다.
- `A2` vs `Lane C/D/E` boundary:
  - host/view/bootstrap miswire, presenter glue, installer wiring, scene composition, launcher-smoke-adjacent UX glue가 first wrong oracle면 `A2`
  - support tree relocation, persistent BGM ownership/registry/continuity, terrain/occupancy legality/query/reservation semantics가 root cause면 handoff
  - 증상이 host/view/bootstrap에 보여도 해결에 `Lane C/D/E` decision이 필요하면 `A2`가 아니라 handoff
- historical continuation row라도 current revision에서 failure shape가 달라졌다면 `A4`로 carry하지 않는다.
- 이런 row는 `shape-changed-from-historical` 태그와 함께 `A1 provisional`로 잠그고 bounded recheck 뒤에 lane을 확정한다.
- 신규 적색 후보가 full rerun `1`회만으로 불확실하면 same-revision bounded recheck를 `1`회 추가한다.
- 기본 recheck는 `targeted reproducer 1회`다.
- targeted reproducer가 없거나 `first wrong oracle`를 못 자르면 same-revision full rerun `1`회 추가를 허용한다.
- confidence:
  - `High`: first wrong oracle 직접 캡처 + owner surface 일치
  - `Medium`: adjacent guard/control로 owner가 충분히 유도됨
  - `Low`: shape-changed row, mixed oracle row, 재확인 대기 row
- `High`, `Medium`만 active queue로 승격한다.
- `Low`는 recheck 전까지 `triaged` 또는 `blocked`로 유지한다.

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
  - direct-play launcher, catalog coverage, plain Play unsupported, stage-0-1/menu mismatch -> `Lane B`
  - support tree 위치, relocation 필요성, consumed asset complete 여부 -> `Lane C`
  - persistent BGM owner, registry, same-root audio flow, cross-scene continuity -> `Lane D`
  - occupancy claim, terrain query, legality, reservation, modifier, traversal/settlement semantics -> `Lane E`
  - wording drift, close note, doc test, CI claim, artifact scope 혼합 -> `Lane F`

## Handoff Row Schema

- `source lane`
- `target lane`
- `row id/test name`
- `reason`
- `blocking claim`
- `required evidence`
- `created at`
- `accepted by`
- `status`

모든 handoff row는 위 필드를 채운다. same-revision evidence 없이 acceptance를 기록하지 않는다.

## Handoff Acceptance And Return Rules

- `pending-acceptance`:
  - target lane이 아직 handoff를 수락하지 않은 상태
  - source lane active queue에서 row를 제거하지 않는다
  - source ledger에는 `triaged` 또는 `blocked`로 남긴다
- `accepted`:
  - target lane owner가 current same-revision artifact를 확인하고 수락한 상태
  - 이 시점에 source row status를 `handed-off`로 바꾼다
- `rejected`:
  - target lane이 reject reason과 required return evidence를 함께 적은 상태
  - source lane은 자동 복귀가 아니라 owner가 새 next action을 적고 재개해야 한다
- `closed`:
  - target lane에서 실제 처리 또는 supersede가 끝난 상태
- stale evidence는 handoff acceptance 근거로 쓸 수 없다.
- historical artifact는 comparison-only reference로만 남긴다.
- source close note는 handed-off row를 계속 `Open Functional Backlog / Handoff`에 남긴다.
- target close note는 accepted row를 `inherited open backlog`로 기록한다.

## Lane F Governance Hygiene Lock

- Lane F close note는 minimum common sections에 더해 아래 섹션을 반드시 포함한다.
  - `Reviewed Truth Sources`
  - `Drift Triage Summary`
  - `Claim Vocabulary Audit`
  - `Template Alignment Result`
- doc drift vs false positive triage order:
  1. failing doc test / grep / audit 재현
  2. `Gameplay-Test-Automation-Guide.md`
  3. `Bounded-Lane-Close-Template.md`
  4. `Post-Stage-Content-Bounded-Lane-Operations.md`
  5. actual close note / example
- actual close note가 상위 truth-source에 맞고 grep/doc-test만 낡았으면 `false positive`
- actual close note 또는 lane-local doc가 상위 truth-source와 다르면 `doc drift`
- 상위 truth-source끼리 충돌하면 `Guide -> Template -> Ops -> example/tests` 순으로 고친다.
- Lane F audit artifact는 아래 필드를 남긴다.
  - `searched paths`
  - `disallowed phrases`
  - `match count`
  - `allowed phrase spot-check`
  - `revision`
  - `date/time`
- `governance hygiene green alone does not close Lane B, Lane A, or any functional lane`

## Lane Bounded Execution Order

1. `Lane F` wording/evidence lock
2. `Lane B` smoke evidence template and counter rule lock
3. `Lane B` soft adoption
4. `Lane B` `Cycle 1` launcher smoke
5. `Lane A` same-revision full baseline re-freeze
6. `Lane A` row import and provisional owner lock
7. `Lane A` low-confidence recheck and handoff proposal
8. `Lane A` active queue open
9. `Lane B` `Cycle 2` launcher smoke
10. `Lane B` hard enforcement review
11. `Lane F` final vocabulary audit and produced-note alignment review
12. `Lane A` kickoff note / live ledger publish
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
  - stage-0-1
  - menu path
  - smoke checklist
  - warning text
  - smoke cycle template
  - counter rule

### Step 3. Direct-Play Cycle 1

- execute:
  - launcher-supported `3` scene smoke
  - exact menu path capture
  - warning / fail-fast capture
  - plain Play workflow classification

### Step 4. Full Baseline Re-Freeze

- inputs:
  - same-revision `./run_tests.sh full`
  - current full XML
  - per-class histogram
  - previous pinned artifact
  - handoff codebook

### Step 5. Lane A Row Import And Handoff Proposal

- import:
  - live row ledger
  - handoff ledger
- lock:
  - row lifecycle
  - source artifact
  - classification confidence
  - next action

### Step 6. Direct-Play Hard Enforcement And Lane A Owner Lock

- enforce:
  - launcher bypass is unsupported misuse
- recover:
  - `A1`
  - `A2`

### Step 7. A3/A4 Recovery

- recover:
  - direct unrelated backlog
  - baseline continuation

### Step 8. Lane F Final Audit And Publication

- audit:
  - claim vocabulary grep
  - template alignment
  - close note section completeness
- publish:
  - Lane A kickoff note
  - Lane F hygiene close note

### Step 9. Broad Recovery Claim Review

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
- pending handoff row는 acceptance 전 source active queue에서 제거하지 않는다.
