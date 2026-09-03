# Enemy Animation Sparse Binding — Migration Tool Retirement Plan

## 1. 문서 상태

- 대상: Slice 4A — one-time production migration tool retirement
- 선행 상태:
  - production 10개 View sparse binding migration 완료
  - legacy non-production View 4개 삭제 완료
  - current inventory: Driver 10 / Binding 8 / Timing 0
  - disposition ledger: MigratedBinding 8 / ApprovedNoBinding 2 / Deleted 4 / LegacyBlocked 0
- 작성일: 2026-09-04
- 범위 상태: 2026-09-04 구현 완료. 실행 결과는
  [Migration Tool Retirement Closeout](./Enemy-Animation-Sparse-Binding-Migration-Tool-Retirement-Closeout.md)에
  기록한다.

이 계획의 목적은 완료된 migration을 다시 실행할 수 있는 mutation surface를 제거하고, 이후에도 필요한
production 계약과 residue 검증만 read-only audit으로 남기는 것이다. Tool 삭제 자체를 inventory 문제의
해결로 간주하지 않으며, 삭제 후 남은 문제는 별도 재감사에서 다시 판정한다.

구현에서는 manifest의 migration approval/digest와 legacy serialization 입력도 함께 제거하고, current
prefab/Driver/Animator/Controller 및 cue/clip identity만 유지했다. manifest와 ledger의 migration 계열
파일명/타입명은 계획대로 rename하지 않았다.

## 2. 결론과 목표 상태

`EnemyAnimationBindingMigrationTool.cs`의 menu, apply service, migration report, serialized mutation helper는
one-time 역할을 마쳤으므로 제거한다. 반면 production semantic manifest와 14-row disposition ledger는
현재 자산 계약을 설명하는 read-only 기준표로 유지한다.

완료 후 상태는 다음과 같아야 한다.

1. Editor에 `Dry Run Production Migration`과 `Apply Approved Production Migration` menu가 없다.
2. production prefab을 자동 저장하거나 Timing Authoring을 자동 제거하는 migration code가 없다.
3. migration 전용 approval digest, report, row status, apply exception, test hook이 없다.
4. production 10개 semantic matrix와 disposition ledger 14행은 유지된다.
5. permanent audit은 prefab의 resolved component를 기준으로 Driver 10 / Binding 8 / Timing 0을 검증한다.
6. deleted prefab GUID 4개와 deleted Jumping material GUID의 serialized inbound reference 0을 검증한다.
7. production binding snapshot, Controller/AOC, effective motion, clip GUID/fileID 계약을 유지한다.
8. legacy-only runtime compatibility는 migration implementation이 아닌 runtime fixture가 검증한다.
9. Prefab, Controller, AOC, FBX, AnimationClip, Scene, ScriptableObject에는 diff가 없다.
10. 제거 완료 후 잔여 문제를 별도 재감사하며, 이 계획만으로 evidence/material/manual 문제를 해결했다고
    주장하지 않는다.

## 3. 계약 분류

### 3.1 StrongContract — 유지

- production 10개 View의 cue/mode/target/timing matrix
- production 8개 root Binding과 Kali/SecBot no-binding disposition
- Driver 10 / Binding 8 / Timing 0의 resolved asset inventory
- 14개 historical disposition과 deleted GUID tombstone
- Controller/AOC, Animator path, effective motion, clip GUID/local file ID identity
- valid new binding의 legacy fallback 금지
- runtime signal, timing, resync, topology, Death 및 DeathMotion 동작
- public legacy timing 타입/API source compatibility
- legacy-only runtime compatibility fixture

### 3.2 CurrentPolicy — 제거

- production migration dry-run/apply menu
- approved dry-run SHA-256 digest와 apply authorization
- `LegacyReady`, `AlreadyMigrated`, `Blocked` migration row status
- migration canonical/human report 및 D 드라이브 report writer
- legacy Timing Authoring을 새 Binding으로 변환하는 serialization code
- production prefab save/reload orchestration
- migration implementation을 위한 `ForTests` hook과 synthetic migration serialization tests

### 3.3 Read-only historical material — 유지

- Slice 2 migration plan과 closeout
- Slice 3 retirement closeout
- production migration manifest의 cue/asset identity rows
- disposition ledger의 Deleted rows와 replacement 설명

Historical 문서의 menu와 tool 설명은 과거 실행 절차로 남길 수 있지만, current architecture entrypoint와
implementation status에는 tool이 retired되었음을 명시한다.

## 4. 변경 범위

### 4.1 제거 대상

- `Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Editor/EnemyAnimationBindingMigrationTool.cs`
- 대응 `.meta`
- 다음 migration-only 타입과 동작:
  - `EnemyAnimationMigrationRowStatus`
  - `EnemyAnimationMigrationRowResult`
  - `EnemyAnimationMigrationReport`
  - `EnemyAnimationMigrationRowApplyException`
  - `EnemyAnimationBindingMigrationService`
  - `EnemyAnimationBindingMigrationTool`
  - dry-run/apply/report/evidence writing
  - `InspectRowForTests`, `ApplyRowForTests`, `ShouldMutateForTests`
  - `ConfigureBindingWithSerializedObject`의 migration-only 호출 경로

### 4.2 유지 대상

- `EnemyAnimationBindingMigrationManifest.cs`
  - production 10-row semantic table
  - 14-row disposition ledger
  - production/deleted GUID와 target identity
- `EnemyAnimationControllerBindingValidator.cs`
- `EnemyAnimationBindingAuthoringEditor.cs`
- runtime binding/schema/Driver
- production semantic contract 및 runtime characterization tests
- deleted GUID/path/meta residue tests
- Controller/AOC/effective clip characterization tests

Manifest의 파일명과 내부 migration 계열 타입명은 이번 단계에서 rename하지 않는다. 이름 정리는 기능
제거와 무관한 churn이므로 후속 current-policy cleanup 후보로만 기록한다.

### 4.3 수정 금지

- production 10개 Enemy View prefab
- Animator Controller/AOC
- FBX/AnimationClip
- Scene/ScriptableObject/catalog/profile asset
- Enemy AI, view lifetime, DeathMotion runtime
- Driver legacy private serialized field
- `EnemyAnimationTimingAuthoring` public source/type/API

Driver legacy private field 제거는 Slice 4의 별도 decommission intent다. Tool retirement와 같은 최종
revision에서 검증할 수는 있지만, 이 계획은 그 필드 제거를 자동 승인하지 않는다.

## 5. 테스트 전환

### 5.1 삭제할 migration implementation tests

- `EnemyAnimationBindingMigrationDryRunTests`
  - deterministic dry-run, approved apply idempotency는 제거되는 service 자체의 계약이다.
- `EnemyAnimationBindingMigrationSerializationTests`
  - legacy prefab 생성, serialized migration, partial migration, apply-before-save failure는 제거되는 mutation
    implementation의 계약이다.

삭제된 production migration code를 test-only clone으로 남기지 않는다. Git history와 Slice 2 evidence가
one-time migration 동작의 기록이다.

### 5.2 유지·전환할 permanent tests

`EnemyAnimationBindingMigrationManifestTests`는 read-only manifest/ledger test로 유지한다.

- production row 10, ledger row 14
- 8 MigratedBinding / 2 ApprovedNoBinding / 4 Deleted / 0 LegacyBlocked
- live row path/GUID가 manifest와 exact match
- deleted row path/meta/GUID resolve 및 serialized reference 0
- target matrix, cue ordering, timing/clip truth table

`EnemyAnimationSparseBindingProductionContractTests`는 migration 상태어를 제거하고 current asset 계약만
검증하도록 바꾼다.

- `AlreadyMigrated` assertion 제거
- reimport 후 Missing Script 0
- root Driver 1
- migrated root Binding 1, Kali/SecBot Binding 0
- Timing Authoring 0
- snapshot과 manifest matrix exact match
- Controller/AOC/effective motion/clip identity 유지

### 5.3 resolved inventory로 교체

현재 YAML `m_Script` GUID count는 Prefab Variant가 상속한 component를 놓칠 수 있으므로 authoritative
inventory로 사용하지 않는다.

permanent inventory test는 다음 순서로 모든 prefab의 resolved representation을 검사한다.

1. `AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })`
2. GUID를 asset path로 변환하고 ordinal 정렬
3. 각 prefab을 `AssetDatabase.LoadAssetAtPath<GameObject>`로 로드
4. inactive child를 포함해 Driver/Binding/Timing component 존재 여부 검사
5. component별 prefab path set을 한 번의 scan으로 생성
6. Driver set은 production manifest 10개와 exact match
7. Binding set은 MigratedBinding 8개와 exact match
8. Timing set은 empty

raw YAML scan은 다음 보조 residue에만 사용한다.

- `.prefab`, `.unity`, `.asset`의 deleted GUID inbound reference
- Scene/ScriptableObject의 Driver/Binding/Timing direct serialized reference
- retired migration/tool symbol 잔존

### 5.4 신규 negative coverage

- allowlist 밖 Prefab Variant가 base prefab의 Driver를 상속하면 inventory가 실패한다.
- deleted prefab GUID를 가진 temporary `.asset` 또는 prefab이 있으면 residue audit이 실패한다.
- production live ledger의 name/path/GUID/disposition이 manifest와 다르면 실패한다.
- deleted ledger row의 `ExpectedAssetExists`, production flag, replacement target이 잘못되면 실패한다.

Fixture가 static table을 직접 바꾸지 못한다면 pure audit input/model을 test assembly에 두거나 audit helper에
row/inventory provider를 주입한다. production mutation service를 재도입해서 negative test를 만들지 않는다.

## 6. 실행 순서

### Phase R0 — Preflight

- HEAD/revision, `git status`, `git diff --stat` 기록
- production prefab 10개 hash 기록
- Driver/Binding/Timing resolved inventory baseline 기록
- deleted GUID 4개 및 Jumping material GUID inbound reference 0 기록
- migration tool과 manifest/test의 exact caller 목록 기록

Gate: 사용자 변경과 retirement 변경을 파일 단위로 구분할 수 있어야 한다.

### Phase R1 — Permanent audit tests first

- resolved prefab inventory test 추가
- Prefab Variant negative test 추가
- deleted GUID inbound negative test 추가
- manifest/ledger full identity negative test 추가
- production contract에서 `DryRun()` 의존 제거

Gate: 새 audit은 current assets에서 통과하며 synthetic invalid input에서 반드시 실패해야 한다.

### Phase R2 — Mutation surface 제거

- migration tool C#과 `.meta` 삭제
- migration-only test fixtures 삭제
- remaining test compile reference를 read-only manifest/audit로 전환
- menu name, service type, apply method, approved digest 사용처 residue scan

Gate: runtime/asset 파일 diff 없이 Editor assembly와 test assembly가 compile되어야 한다.

### Phase R3 — Documentation/evidence 정리

- implementation plan Slice 4A 상태를 retired로 갱신
- Architecture README에 current tool 부재와 read-only manifest 상태 반영
- Slice 2/3 closeout은 historical snapshot임을 유지
- retirement closeout 또는 evidence index 작성
- 기존 core 결과 `111/111` 표기를 `111 total / 107 passed / 4 skipped / 0 failed`로 정정

Gate: historical 실행 기록과 current architecture 설명이 충돌하지 않아야 한다.

### Phase R4 — 제거 후 독립 재감사

Tool 제거 완료를 다른 문제의 자동 해결로 간주하지 않고 다음을 다시 검사한다.

- resolved Driver/Binding/Timing inventory와 Prefab Variant 탐지
- deleted GUID inbound reference
- production 10 prefab 및 protected asset zero-diff
- runtime/catalog fallback 부재
- manifest/ledger identity
- missing targeted artifact
- BlackEye inactive material baseline failure
- manual Editor/reimport evidence
- current 문서 drift와 forbidden green claim

Gate: 재감사 결과에서 새 High/Medium finding을 별도로 보고하고 필요한 후속 intent를 다시 승인받는다.

## 7. 예상 파일 변경

삭제:

- `Gameplay_EnemyPresentation/Editor/EnemyAnimationBindingMigrationTool.cs`
- 대응 `.meta`

수정:

- `Gameplay_Tests/.../EnemyAnimationBindingMigrationTests.cs`
- `Gameplay_Tests/.../EnemyAnimationSparseBindingAssetCharacterizationTests.cs`
- 필요 시 read-only audit helper와 `.meta`
- `Enemy-Animation-Sparse-Binding-Implementation-Plan.md`
- `Enemy-Animation-Sparse-Binding-Slice3-Legacy-View-Retirement-Closeout.md`
- `Docs/Architecture/README.md`
- 완료 후 retirement closeout/evidence index

내용 diff가 없어야 하는 파일군:

- production Enemy View prefab 10개
- Controller/AOC/FBX/AnimationClip
- Scene/`.asset`
- runtime binding/Driver/Timing public API

## 8. Validation

구현 시 실제 fixture 이름을 확인해 다음 순서로 실행한다.

```bash
./run_tests.sh full --filter EnemyAnimationBindingMigrationManifestTests
./run_tests.sh full --filter EnemyAnimationSparseBindingProductionContractTests
./run_tests.sh full --filter EnemyAnimationSparseBindingAssetCharacterizationTests
./run_tests.sh full --filter EnemyAnimationBindingEditorValidationTests
./run_tests.sh full --filter EnemyAnimationBindingDispatchTests
./run_tests.sh full --filter EnemyAnimationSparseBindingRuntimeScenarioTests
./run_tests.sh full --filter EnemyViewAnimatorControllerContractTests
./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests
./run_tests.sh full --filter EnemyInactiveMaterialAuthoringTests
./run_tests.sh core
```

추가 static gates:

```bash
rg -n "Dry Run Production Migration|Apply Approved Production Migration|EnemyAnimationBindingMigrationService|ApplyRowForTests|InspectRowForTests" Assets
git diff --name-only cb0cbdbbecdd09c19b370692e73e90b8fb21483d -- '*.prefab' '*.controller' '*.overrideController' '*.fbx' '*.anim' '*.unity' '*.asset'
git diff --check
```

첫 번째 residue scan은 결과 0이어야 한다. asset diff scan은 비교 기준 revision을 구현 시작 checkpoint로
고정하며 결과 0이어야 한다.

UI 변경이 없으므로 `ui` lane은 요구하지 않는다. broad unfiltered `full`을 실행하지 않았다면 그대로
기록하고 project-wide green을 주장하지 않는다.

## 9. Hold 조건

- resolved inventory가 Driver 10 / Binding 8 / Timing 0이 아님
- allowlist 밖 inherited Driver/Binding/Timing prefab 발견
- deleted GUID inbound reference 발견
- production prefab 또는 protected asset diff 발생
- permanent contract test가 migration service 없이는 표현되지 않음
- Controller/effective clip validation을 보존하려면 mutation code가 필요함
- legacy-only runtime compatibility가 synthetic migration test에만 존재함
- production runtime characterization 또는 core 회귀
- 신규 fixture owning-stage matching count가 0
- current 사용자 변경과 retirement diff를 안전하게 분리할 수 없음

Hold가 발생하면 migration tool을 되살리기보다 누락된 read-only contract seam을 먼저 설계한다.

## 10. Evidence

신규 evidence 후보:

`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-tool-retirement/<run-id>/`

- `00-preflight/`: revision, status, tool callers, production hashes
- `01-tests-first/`: variant/deleted-reference negative evidence
- `02-retirement/`: removed symbol/path/meta audit
- `03-targeted/`: read-only manifest/inventory/production contract XML과 logs
- `04-production-playmode/`
- `05-core/`
- `06-final-audit/`: protected asset zero-diff, remaining findings, non-claims

성공 artifact와 tests-first failure, unrelated baseline failure를 구분한다. 이전 Slice 3 evidence의 누락을
새 run이 실행되지 않은 것처럼 소급 포장하지 않는다.

## 11. Rollback

Tool retirement rollback은 retirement commit 전체의 Git revert다. 다음을 함께 되돌린다.

- migration tool 및 `.meta`
- migration implementation tests
- permanent audit 전환
- retirement 문서 상태

production prefab은 retirement commit에서 변경하지 않으므로 rollback 대상이 아니다. 과거 production
migration 자체를 되돌려야 한다면 implementation plan에 기록된 역순 commit revert 절차를 사용한다.
삭제된 tool을 raw file copy로 복구하거나 menu만 수동 재작성하는 방식은 rollback으로 인정하지 않는다.

## 12. Commit 경계

Tool 제거, migration-only test 제거, permanent audit 대체, current 문서 상태 갱신을 하나의 decommission
intent로 묶는다.

권장 제목:

```text
refactor: Gameplay/EnemyAnimation - one-time migration tool 퇴역
```

본문에는 최소 다음을 기록한다.

- production migration과 legacy View 처분이 완료되어 mutation surface를 제거한 이유
- manifest/ledger와 permanent read-only audit을 유지한 방식
- Prefab Variant와 deleted GUID negative coverage
- production/protected asset zero-diff 및 runtime/core evidence
- retirement commit 전체 revert가 rollback 경로임

실제 commit 전에는 `commit-push-workflow` skill과 `AI_GIT_COMMIT_RULES.md`를 따른다.

## 13. 완료 Gate

- migration tool C#과 `.meta` 부재
- dry-run/apply Editor menu 부재
- migration service/report/status/apply/test-hook symbol residue 0
- migration-only tests 부재
- production manifest 10행 및 disposition ledger 14행 유지
- resolved inventory Driver 10 / Binding 8 / Timing 0
- Prefab Variant inheritance negative test 통과
- deleted prefab/material GUID inbound reference 0 및 negative test 통과
- production cue/mode/target/timing snapshot 통과
- Controller/AOC/effective motion/clip identity 통과
- production prefab 10개 및 protected asset unexpected diff 0
- production PlayMode characterization과 core failure 0
- evidence 수치가 XML과 일치
- 제거 후 독립 재감사 결과와 남은 문제를 별도 기록

이 Gate가 닫히기 전에는 Tool 관련 finding이 해결됐다고 주장하지 않으며, legacy private serialized field
제거 또는 다른 cleanup을 자동으로 시작하지 않는다.
