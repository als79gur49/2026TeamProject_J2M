# Enemy Animation Sparse Binding — Migration Tool Retirement Closeout

## 상태

- 완료일: 2026-09-04
- 범위: Slice 4A one-time production migration tool retirement
- 상태: 코드·계약 retirement 완료; R0 contemporaneous evidence completeness에는 아래 제한이 있음
- current inventory: Driver 10 / Binding 8 / Timing 0
- disposition: MigratedBinding 8 / ApprovedNoBinding 2 / Deleted 4 / LegacyBlocked 0
- 기준 revision: `cb0cbdbbecdd09c19b370692e73e90b8fb21483d`
- rollback: 이 retirement commit 전체를 Git revert한다.

## 구현 결과

- `EnemyAnimationBindingMigrationTool.cs`와 `.meta`를 삭제했다.
- dry-run/apply menu, apply service, report/row status/approval digest, serialization mutation helper와
  `ForTests` hook을 제거했다.
- migration-only dry-run 및 synthetic serialization fixture를 삭제했다.
- production semantic manifest 10행과 disposition ledger 14행은 read-only current asset 계약으로 유지했다.
  approval/digest와 legacy timing/Driver serialization 입력은 current 계약이 아니므로 manifest에서 제거했다.
- `EnemyAnimationSparseBindingAudit`이 모든 prefab의 resolved representation을 한 번 스캔해 inherited
  component까지 포함한 Driver/Binding/Timing path set을 검증한다.
- allowlist 밖 Prefab Variant가 Driver를 상속하는 경우, deleted GUID가 temporary serialized asset에
  유입되는 경우, live/deleted ledger identity가 변하는 경우를 각각 negative fixture로 고정했다.
- deleted prefab GUID 4개와 Jumping 전용 inactive material GUID를 동일한 serialized-reference audit에
  포함했다.
- production prefab, Controller/AOC/FBX/AnimationClip, Scene/ScriptableObject와 runtime API는 변경하지 않았다.

## Evidence 귀속과 R0 제한

초기 R0 checkpoint는 base revision, branch, 당시 식별한 사용자 변경, production prefab 10개 SHA-256을
contemporaneous하게 기록했다. 다음 항목은 R0 시점에 완전한 원문 evidence로 수집되지 않았다.

- `git status --short --branch` 원문
- `git diff --stat` 원문
- resolved Driver 10 / Binding 8 / Timing 0 baseline
- deleted prefab/material GUID inbound reference 0 baseline
- migration tool의 exact caller graph

이 누락 자료를 사후 실행 결과로 R0 evidence라고 소급 표기하지 않는다. 기존 checkpoint의
"Tool callers" 목록은 exact caller graph가 아니라 migration 관련 파일/use-site 식별 목록이었다.

다음 post-retirement 결과는 R0를 대체하지 않는 compensating evidence다.

- base-to-retirement protected asset diff 0
- final resolved inventory Driver 10 / Binding 8 / Timing 0
- deleted GUID inbound reference 0
- Enemy Presentation runtime diff 0
- targeted runtime characterization 및 core 결과

최종 tested commit/tree와 validation 전후 clean status는 기존 evidence run을 덮어쓰지 않고 후속 corrective
evidence run에 post-commit으로 기록한다.

## 초기 retirement 검증 결과

| 명령 | 결과 |
|---|---|
| manifest/production/inventory tests-first 필터 | EditMode 11/11, PlayMode matching 0, Windows full build 통과 |
| `./run_tests.sh full --filter EnemyAnimation` | EditMode 103/103, PlayMode matching 0 |
| Controller contract + runtime characterization 필터 | EditMode 8/8, PlayMode 18/18 |
| `./run_tests.sh core` | EditMode 254/254, PlayMode 111 total / 107 passed / 4 skipped / 0 failed |

## 후속 review hardening

retirement 후 독립 검토에서 synthetic fixture가 matcher/validator에는 도달하지만 production discovery
단계를 직접 통과하지 않는 coverage gap과 live ledger identity의 필드별 negative matrix 누락을 확인했다.
production mutation code를 되살리지 않고 다음과 같이 permanent audit fixture만 보강했다.

- inherited Driver Prefab Variant는 `FindAllPrefabAssetPaths()`의 실제 전체 discovery 결과로 검사한다.
- deleted prefab GUID 4개와 deleted Jumping material GUID 1개는 `Assets` 아래 단일 temporary `.asset`에서
  `FindSerializedAssetPaths("Assets")`와 GUID matcher 전체 경로로 검사한다.
- live ledger의 name/path/GUID/disposition을 각각 독립적으로 변조해 거부 결과를 고정한다.
- raw GUID fixture는 Unity import 전에 제거하며 fixture root와 `.meta` residue 0을 별도 검사한다.

Corrective pre-commit 검증 결과:

| 명령 | 결과 |
|---|---|
| `./run_tests.sh full --filter EnemyAnimationBindingMigrationManifestTests` | EditMode 7/7, PlayMode matching 0 |
| `./run_tests.sh full --filter EnemyAnimationSparseBindingAssetCharacterizationTests` | EditMode 6/6, PlayMode matching 0 |
| `./run_tests.sh full --filter EnemyAnimation` | EditMode 107/107, PlayMode matching 0 |
| `./run_tests.sh full --filter EnemyViewAnimator` | EditMode 8/8, PlayMode 18/18 |
| `./run_tests.sh core` | EditMode 254/254, PlayMode 111 total / 107 passed / 4 skipped / 0 failed |
| `./run_tests.sh full --filter EnemyInactiveMaterialAuthoringTests` | EditMode 8 total / 6 passed / 2 failed; 기존 BlackEye baseline |

최종 same-revision 판정은 corrective commit 후 clean HEAD에서 다시 생성한 XML/log와 external evidence의
tested commit/tree 기록을 기준으로 한다. 위 pre-commit 결과만으로 final revision을 주장하지 않는다.

Static 재감사 결과:

- migration menu/service/report/status/apply/test-hook symbol residue: 0
- deleted prefab/material GUID의 `.prefab`/`.unity`/`.asset` inbound reference: 0
- resolved prefab inventory: Driver 10 / Binding 8 / Timing 0
- 기준 revision 대비 Prefab, Controller/AOC, FBX/AnimationClip, Scene/ScriptableObject diff: 0
- Enemy Presentation runtime diff: 0
- `git diff --check`: 통과

## 분리된 실패와 남은 작업

`EnemyInactiveMaterialAuthoringTests`는 EditMode 8 total / 6 passed / 2 failed였다. 두 실패는 기존
BlackEye package Lit duplicate의 `_EmissionColor` 및 source-candidate fidelity 문제이며, 관련 material,
source, test에는 이번 retirement diff가 없다. 따라서 tool retirement 회귀가 아닌 기존 baseline으로
분리하며 해결됐다고 주장하지 않는다.

- broad unfiltered `full`은 실행하지 않았으므로 project-wide/full-lane green을 주장하지 않는다.
- UI 변경이 없어 `ui` lane은 실행하지 않았다.
- 별도 수동 Editor screenshot/evidence는 생성하지 않았다. 자동 reimport/current composition 검증만 수행했다.
- 이전 Slice 3의 누락 evidence를 이번 run으로 소급 대체하지 않는다.
- Driver legacy private serialized field 제거는 별도 Slice 4B intent이며 이 작업이 승인하거나 시작하지 않았다.
- manifest/ledger의 migration 계열 파일명과 타입명 정리는 후속 current-policy cleanup 후보로 남긴다.

## Evidence

Initial evidence root:
`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-tool-retirement/20260904-042826-KST/`

- `01-tests-first/`: 삭제 직후 stale generated `.csproj` failure
- `01-tests-first-rerun/`: cold-import compile feedback
- `01-tests-first-pass/`: permanent audit EditMode 11/11 성공 artifact
- `03-targeted/`: EnemyAnimation EditMode 103/103
- `03-targeted-final/`: strengthened permanent audit EditMode 11/11
- `04-production-playmode/`: Controller EditMode 8/8 및 runtime PlayMode 18/18
- `04-inactive-material/`: 기존 BlackEye baseline failure 2건
- `05-core-final/`: final EditMode 254/254, PlayMode 111 total / 107 passed / 4 skipped / 0 failed

Corrective evidence root:
`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-tool-retirement/20260904-160640-KST/`

- `00-context/`: corrective scope와 initial run 보존 경계
- `01-targeted/`: strengthened permanent audit와 EnemyAnimation aggregate
- `02-runtime/`: Controller/effective motion 및 runtime characterization
- `03-core/`: core EditMode/PlayMode
- `04-static/`: retired symbol, deleted GUID, protected asset/runtime diff, temporary residue audit
- `05-known-baseline/`: 기존 BlackEye baseline failure 2건
- `06-final-audit/`: post-commit tested revision/tree/status와 artifact checksum

성공 XML/log, tests-first failure와 unrelated baseline failure는 서로 다른 디렉터리에 보존한다.
