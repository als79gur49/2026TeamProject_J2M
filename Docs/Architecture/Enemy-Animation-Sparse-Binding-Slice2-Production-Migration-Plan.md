# Enemy Animation Sparse Binding — Slice 2 Production Migration Plan

## 1. 문서 상태

- 대상 Slice: Slice 2 — production 10개 Enemy View migration
- 선행 기준:
  - `Enemy-Animation-Sparse-Binding-Implementation-Plan.md`
  - Slice 0 characterization
  - Slice 1 runtime/schema/Editor 구현과 최종 검증 evidence
- 작성일: 2026-09-03
- 범위 상태: 계획만 작성하며 이 문서 작성 시점에는 prefab migration을 실행하지 않는다.

이 문서는 Slice 1에서 추가한 sparse cue binding runtime을 production Enemy View에 적용하는 실행 계획이다. 목표는 기존 animation 결과를 바꾸는 것이 아니라, production prefab의 flat legacy authoring을 명시적인 cue binding으로 옮기고 같은 revision의 전후 동등성을 증명하는 것이다.

## 2. 목표와 완료 결과

Slice 2가 끝나면 다음 상태여야 한다.

1. production 10개 prefab이 manifest에 정확히 한 번씩 기록된다.
2. BlackEye, Startis, RocketFace, Astreton, DrSaturn, JPeter, Sunwheel, Nebulous에는 유효한 root `EnemyAnimationBindingAuthoring`이 정확히 하나 존재한다.
3. Kali와 SecBot은 `ApprovedNoBinding`으로 기록되고 binding component가 없다.
4. production prefab의 기존 `EnemyAnimationTimingAuthoring` component는 제거된다.
5. 새 binding이 있는 View는 cue별 legacy fallback 없이 new snapshot만 사용한다.
6. 기존 Controller/AOC, Animator 경로, clip GUID/fileID, timing 결과와 시각 동작이 유지된다.
7. Driver의 legacy serialized scalar field와 public legacy timing 타입/API는 그대로 남는다.
8. production contract test는 private legacy field가 아니라 cue/mode/target/timing matrix를 검증한다.
9. Controller, AOC, FBX, AnimationClip, Scene, ScriptableObject에는 의도한 diff가 없다.
10. migration 전체가 하나의 production migration commit을 Git revert하여 복구 가능하다.

Slice 2 직후의 expected global inventory는 다음과 같다.

- Driver prefab: 14개 유지
- production Binding Authoring: 8개
- production Timing Authoring: 0개
- global Timing Authoring prefab reference: non-production 4개만 잔존
- non-production disposition: Slice 3까지 pending 또는 명시적 blocker 상태 허용

따라서 Slice 2에서는 global Timing Authoring reference 0이나 `LegacyBlocked == 0`을 요구하지 않는다. 이 두 조건은 비-production 처리가 끝난 뒤 Slice 4 진입 전에 판단한다.

## 3. 계약 분류

### 3.1 StrongContract

- Slice 0과 Slice 1에서 고정한 signal counter, 처리 결과, 중복 억제
- timing 계산과 one-shot dispatch의 독립성
- State pending 1-slot/last-write-wins와 Trigger no-pending
- 일반 resync의 signal 불변 및 Jump topology 전용 normalized-time 보존
- suppression 중 계산 timing 유지, 실제 `Animator.speed`만 0
- Utility exact cue timing과 종료 후 restore
- Death speed 1, duration 0 및 shared/typed/obsolete 진입 경로
- JPeter Summon recovery의 counter-only/no-visual 결과
- production View의 cue 의미 family와 실제 state/trigger target
- Animator/Controller/AOC 및 clip GUID/fileID 보존
- valid new binding의 legacy fallback 금지
- 기존 public timing 타입/API source compatibility
- presentation의 비권위성

### 3.2 MigrationBaseline

다음 값은 migration 전후 동등성을 증명하는 같은-revision baseline이다. 장기 animation tuning을 영구 금지하는 값으로 사용하지 않는다.

- 현재 duration과 reference clip
- 현재 View-level crossfade
- 현재 Animator transform path/local file ID
- 현재 Controller/AOC GUID/local file ID
- 현재 legacy scalar의 raw YAML 존재 여부와 Unity resolved value

### 3.3 CurrentPolicy

- manifest row와 binding row의 내부 C# 타입명
- migration tool의 menu 경로와 report 표현 형식
- dry-run report의 정렬 및 사람이 읽는 메시지
- Editor test fixture 내부 생성 방식

## 4. 범위

### 4.1 포함

- production 10개 prefab의 read-only inventory와 baseline capture
- Editor-only immutable migration manifest
- dry-run 및 apply를 분리한 one-time Editor migration tool
- production 8개 prefab의 sparse binding 추가
- Kali/SecBot의 명시적 no-binding disposition
- production prefab의 legacy Timing Authoring component 제거
- production semantic contract, inventory, residue, serialization 테스트
- 기존 production runtime characterization의 new-binding 전환
- Architecture 문서와 evidence 기록

### 4.2 제외

- 비-production 4개 prefab migration
  - `EnemyView_Attacking`
  - `EnemyView_NonAttacking`
  - `EnemyView_Jumping`
  - `EnemyView_PrototypeGravityFieldChaser`
- `EnemyView_Jumping` repair/remove/archive 결정
- Animator Controller/AOC graph 변경
- FBX 또는 AnimationClip 수정
- Scene 또는 ScriptableObject 수정
- Enemy AI, Death logic, View lifetime, DeathMotion 구현 변경
- raw YAML 일괄 치환
- Driver legacy serialized field 제거
- `EnemyAnimationTimingAuthoring` public source/type/API 제거
- migration mutation tool 제거

## 5. 시작 Gate

Prefab migration을 시작하기 전에 다음 조건을 모두 만족해야 한다.

1. Slice 1 변경이 별도 checkpoint revision으로 식별 가능하다.
2. 현재 worktree의 기존 사용자 변경과 Slice 2 변경을 파일 단위로 구분할 수 있다.
3. Slice 1 최종 evidence에서 다음이 확인된다.
   - binding 묶음 48/48
   - runtime scenario 19/19
   - production PlayMode characterization 13/13
   - core 실패 0
4. production prefab YAML에는 아직 새 binding component가 없다.
5. AOC flatten/effective clip characterization이 현재 Unity version에서 유지된다.
6. D 드라이브 evidence 경로를 사용할 수 있다.
7. 사용자 또는 asset owner가 production 10개 exact dry-run diff를 확인하고 apply를 승인한다.

하나라도 만족하지 않으면 inventory만 수행하고 manifest/apply 작업은 시작하지 않는다.

## 6. Production allowlist

아래 경로만 Slice 2의 prefab mutation 대상 또는 no-binding 확인 대상이다.

공통 디렉터리:

`Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/`

| Prefab | Slice 2 disposition | Binding component | Timing component |
|---|---|---:|---:|
| `EnemyView_BlackEye.prefab` | `MigratedBinding` | 추가 | 제거 |
| `EnemyView_Startis.prefab` | `MigratedBinding` | 추가 | 제거 |
| `EnemyView_RocketFace.prefab` | `MigratedBinding` | 추가 | 제거 |
| `EnemyView_Astreton.prefab` | `MigratedBinding` | 추가 | 제거 |
| `EnemyView_DrSaturn.prefab` | `MigratedBinding` | 추가 | 제거 |
| `EnemyView_JPeter.prefab` | `MigratedBinding` | 추가 | 제거 |
| `EnemyView_Sunwheel.prefab` | `MigratedBinding` | 추가 | 제거 |
| `EnemyView_Kali.prefab` | `ApprovedNoBinding` | 금지 | 기존 상태 확인 |
| `EnemyView_SecBot.prefab` | `ApprovedNoBinding` | 금지 | 기존 상태 확인 |
| `EnemyView_Nebulous.prefab` | `MigratedBinding` | 추가 | 제거 |

Inventory 전체 baseline은 다음과 같다.

- `EnemyAnimatorDriver` 참조 prefab: 14
- `EnemyAnimationTimingAuthoring` 참조 prefab: 12
- production disposition: 10
- non-production disposition: 4

새로운 15번째 Driver prefab, 새로운 production View, Scene/`.asset` 직접 참조가 발견되면 자동 migration을 Hold한다.

## 7. Production target matrix

Manifest에는 아래 semantic target과 실제 prefab에서 읽은 path/GUID/fileID를 함께 기록한다.

| View | Cue | Mode | Target / sustained | Migration timing | Crossfade |
|---|---|---|---|---:|---:|
| BlackEye | ActionWindup | State | `Windup` | 현행 | 0.001 |
|  | ActionRecovery | State | `Recover` | 현행 |  |
|  | Hit | Trigger | `Hit` | 금지 |  |
|  | Death | Trigger | `Death` | 금지 |  |
| Startis | Hit | Trigger | `Hit` | 금지 | -1 |
|  | Death | Trigger | `Death` | 금지 |  |
| RocketFace | ChargeWindup | State | `Windup` | 0.4 | 0.001 |
|  | ChargeActive | State | `Charge` | 금지 |  |
|  | ChargeRecovery | State | `Recover` | 0.4 |  |
|  | Hit | Trigger | `Hit` | 금지 |  |
|  | Death | Trigger | `Death` | 금지 |  |
| Astreton | JumpWindup | State | `JumpWindup` | 0.5 | 0.001 |
|  | JumpAirborne | State | `JumpAirborne` | 1.35 |  |
|  | JumpLanding | State | `Move` | 금지 |  |
|  | ActionExecute | Trigger | `Attack` | 금지 |  |
|  | Hit | Trigger | `Hit` | 금지 |  |
|  | Death | Trigger | `Death` | 금지 |  |
| DrSaturn | UtilityWindup | Trigger | `Windup` | 0.5 | -1 |
|  | UtilityRecovery | Trigger | `Recover` | 0.5 |  |
|  | Hit | Trigger | `Hit` | 금지 |  |
|  | Death | Trigger | `Death` | 금지 |  |
| JPeter | Hit | Trigger | `Hit` | 금지 | -1 |
|  | Death | Trigger | `Death` | 금지 |  |
| Sunwheel | Hit | Trigger | `Hit` | 금지 | -1 |
|  | Death | Trigger | `Death` | 금지 |  |
| Kali | - | - | `ApprovedNoBinding` | - | - |
| SecBot | - | - | `ApprovedNoBinding` | - | - |
| Nebulous | GlideWindup | State | `Fly_Start` | 0.25 | 0 |
|  | GlideActive | State | `Fly_Loop` | 금지 |  |
|  | GlideRecovery | State | `Fly_Done` | 0.25 |  |

`현행` 값은 migration preflight에서 실제 duration과 reference clip을 읽어 manifest 상수로 확정한다. 새 이름이나 clip을 추정하지 않는다.

## 8. Manifest 설계

### 8.1 위치와 형식

신규 후보:

- `Gameplay_EnemyPresentation/Editor/EnemyAnimationBindingMigrationManifest.cs`

Manifest는 Editor assembly 안의 immutable C# table로 둔다.

- ScriptableObject asset을 만들지 않는다.
- runtime assembly가 manifest를 참조하지 않는다.
- schema version을 상수로 둔다.
- prefab path/GUID는 ordinal 비교한다.
- row와 binding collection은 생성 후 수정할 수 없게 한다.
- production row는 항상 명시적으로 작성하며 legacy field에서 자동 생성하지 않는다.

### 8.2 Manifest row 필드

- schema version
- prefab path와 prefab GUID
- production 여부
- disposition: `MigratedBinding` 또는 `ApprovedNoBinding`
- 승인 상태
- Driver script GUID/local file ID
- raw legacy field 존재 여부
- Unity 로드 후 legacy resolved value
- Timing Authoring object reference와 root 일치 여부
- Animator explicit-null 여부
- resolved Animator transform path/local file ID
- assigned Controller/AOC GUID/local file ID
- 기존 duration/crossfade
- reference clip GUID/fileID
- 목표 cue/mode/target/sustained state/timing
- 목표 binding row 순서
- 예상 Controller state/parameter/transition 계약

### 8.3 Manifest invariant tests

- schema version 양수
- production row 정확히 10개
- path/GUID 중복 없음
- allowlist 외 path 없음
- `MigratedBinding` 8개, `ApprovedNoBinding` 2개
- Kali/SecBot target binding 0개
- cue 중복 없음
- target matrix와 정확히 일치
- State 존재 시 crossfade finite `>= 0`
- State 부재 시 crossfade 정확히 `-1`
- timing 금지 cue에 duration/clip 없음
- timing override의 duration/clip truth table 충족
- reference clip GUID/fileID가 모두 명시됨
- binding order deterministic

## 9. Migration tool 설계

### 9.1 신규 후보

- `Gameplay_EnemyPresentation/Editor/EnemyAnimationBindingMigrationTool.cs`
- 필요하면 pure report/model을 별도 Editor 파일로 분리

### 9.2 명시적 명령

도구는 최소 두 진입점을 분리한다.

- `Dry Run Production Migration`
- `Apply Production Migration`

Dry-run과 apply가 같은 service를 사용하되 저장 권한만 다르게 한다. 기본 동작은 dry-run이다.

### 9.3 Two-pass 실행

#### Pass A — 전 production 사전검증

10개 prefab 전부에 대해 저장 없이 다음을 수행한다.

1. path/GUID allowlist 확인
2. `PrefabUtility.LoadPrefabContents`로 로드
3. Driver, Timing Authoring, Binding Authoring 구조 확인
4. raw baseline과 Unity resolved value 비교
5. Animator transform과 Controller/AOC 식별
6. manifest 목표 snapshot 생성
7. catalog 및 Controller validator 실행
8. clip GUID/fileID와 assigned controller effective clip 확인
9. 예상 semantic diff 생성
10. prefab contents unload

10개 중 하나라도 실패하면 apply 가능 상태를 만들지 않는다.

#### Pass B — 승인된 apply

Pass A 전체 성공과 동일 manifest/schema version이 확인된 경우에만 다음을 수행한다.

1. production 8개 root에 binding component 생성
2. manifest 순서대로 binding 구성
   - `SerializedObject`로 private serialized binding array와 crossfade를 기록한다.
   - test 전용 `ConfigureForTests`를 production migration 도구에서 호출하지 않는다.
   - clip은 GUID로 asset path를 구한 뒤 local file ID가 manifest와 일치하는 `AnimationClip`만 연결한다.
3. `CreateSnapshot()`과 Controller validator 실행
4. 해당 production root의 Timing Authoring 제거
   - Driver의 serialized Timing Authoring reference도 reload 후 null인지 확인한다.
5. Kali/SecBot 무변경 확인
6. prefab 저장
7. 즉시 reload
8. semantic matrix와 GUID/fileID 재검증
9. 결과 report 기록

저장 중 한 prefab이라도 실패하면 이후 prefab 저장을 중단하고 Hold한다. 도구가 raw YAML을 되쓰거나 자동 Git rollback을 수행해서는 안 된다. 이미 저장된 대상과 실패 대상을 report에 명시하고 사용자가 migration 전 checkpoint 기준으로 복구 방향을 결정한다.

### 9.4 반복 실행 정책

- 이미 목표 binding과 정확히 일치하는 prefab은 `AlreadyMigrated`로 보고한다.
- 일부만 일치하거나 Timing Authoring이 남아 있으면 hard failure다.
- 새 binding이 비었거나 invalid하면 hard failure다.
- child/disabled/duplicate binding은 hard failure다.
- manifest와 다른 cue를 추가하거나 기존 cue를 추론해 보완하지 않는다.
- apply 완료 후 도구 재실행은 모든 row가 `AlreadyMigrated`여야 한다.

## 10. Test-first 작업 순서

### Phase 2.0 — Slice 1 checkpoint

- current diff와 user-owned 변경 구분
- Slice 1 revision/evidence 기록
- production prefab이 legacy-only인지 재확인
- 작업 전 prefab hash/name-status 기록

Gate: Slice 2 변경과 Slice 1 변경을 안전하게 분리할 수 있어야 한다.

### Phase 2.1 — Inventory/manifest tests

먼저 실패하는 테스트를 추가한다.

- production allowlist 10개
- 전체 disposition baseline 14개
- manifest schema/uniqueness/order
- raw/resolved baseline 일치
- Kali/SecBot `ApprovedNoBinding`
- JPeter Summon recovery no-binding/no-visual
- Controller/AOC 및 effective clip baseline

Gate: prefab을 수정하지 않은 상태에서 inventory와 manifest test가 통과해야 한다.

### Phase 2.2 — Dry-run service

- dry-run semantic diff model 구현
- source/target 비교
- no-mutation fingerprint test
- unknown/duplicate/partial row hard failure
- 모든 production row의 dry-run report 생성

Gate: dry-run 전후 `git diff --name-only`와 prefab content hash가 동일해야 한다.

### Phase 2.3 — Apply service와 synthetic migration tests

임시 synthetic prefab으로 다음을 먼저 검증한다.

- legacy State View migration
- Trigger-only View migration
- State+Trigger 혼합 migration
- Timing Authoring 제거
- no-binding disposition 무변경
- invalid Controller/clip에서 저장 전 실패
- reload 후 snapshot 동등성
- 두 번째 실행의 idempotent `AlreadyMigrated`
- partial migration hard failure

Gate: production prefab은 아직 수정하지 않는다.

### Phase 2.4 — Production apply

- 승인된 dry-run report를 기준으로 production 10개 처리
- 8개 binding 추가, 2개 no-binding 확인
- 저장/reload/post-validate
- 예상 파일 외 diff 즉시 Hold

Gate: production semantic contract와 asset identity 검증이 모두 통과해야 한다.

### Phase 2.5 — Contract test 전환

- production test를 cue/mode/target/timing matrix로 전환
- raw legacy private field reflection 기대 제거
- Startis/BlackEye Timing Authoring 존재 가정 제거
- production runtime characterization을 new binding 경로로 검증
- synthetic legacy-only fixture 유지
- Slice 0 signal/timing/topology 결과 재사용

Gate: new production과 legacy compatibility를 별도 fixture로 모두 검증해야 한다.

### Phase 2.6 — Manual Editor/reimport 확인

- 대표 Inspector 표본 확인
- reimport 후 Missing Script 없음
- Controller/FBX/AnimationClip diff 없음
- clip GUID/fileID 유지
- 주요 animation visual parity 확인

Gate: 수동 결과와 screenshot/note evidence가 있어야 한다.

## 11. 테스트 배치

### Core / Category `Core`

- 기존 catalog/snapshot tests

Manifest는 Editor assembly에 있으므로 Core에 중복 mirror를 만들지 않는다. Core에는 AssetDatabase, PrefabUtility, private-field reflection을 넣지 않는다.

### Infrastructure / Category `Full`

- production inventory와 GUID/local file ID
- manifest schema/uniqueness/order/target matrix
- manifest/raw/resolved value 비교
- dry-run no-mutation
- migration serialization/reload
- Timing Authoring production residue
- post-migration global Timing Authoring reference가 non-production 4개로만 제한되는지 확인
- Controller/AOC graph와 effective clip
- Inspector semantic matrix

### Scenario / Category `Full`

- migrated new-binding dispatch
- timing/one-shot 독립성
- Recovery+Hit
- Utility exact cue
- Death 3경로
- Astreton Jump pending/resync/topology
- JPeter Summon no visual
- legacy-only compatibility fixture

### PlayMode / Category `Full`

- production 10 View runtime characterization
- 실제 Astreton Jump state/landing/topology
- 실제 DrSaturn Utility trigger/timing
- 실제 JPeter Summon recovery counter-only
- 실제 Nebulous Glide state/timing
- DeathMotion pose freeze 비회귀

각 신규 fixture는 owning stage matching count가 0보다 커야 한다.

## 12. 예상 파일 변경

### 신규 Editor 파일 후보

- `Gameplay_EnemyPresentation/Editor/EnemyAnimationBindingMigrationManifest.cs`
- `Gameplay_EnemyPresentation/Editor/EnemyAnimationBindingMigrationTool.cs`
- 필요한 `.meta`

### Production prefab

- allowlist의 production 10개만 확인 대상
- 실제 YAML mutation은 binding 대상 8개와 Timing Authoring 제거가 필요한 대상에 한정
- Kali/SecBot은 내용 diff 0이어야 함

### 테스트

- production semantic contract fixture
- migration manifest/inventory fixture
- migration serialization/residue fixture
- 기존 Controller contract, prefab scaffold, presentation authoring test
- 기존 runtime characterization PlayMode test
- timing/coordinator/mapper 관련 fixture

### 문서

- 이 실행 계획
- 상위 implementation plan의 Slice 2 상태
- Architecture README 링크
- 완료 후 별도 closeout 또는 evidence index

### 수정 금지

- Animator Controller/AOC
- FBX/AnimationClip
- Scene
- ScriptableObject/catalog/profile asset
- Enemy AI
- Death/View lifetime/DeathMotion runtime
- Driver legacy private field 제거
- public legacy timing source/type/API 제거
- 비-production prefab

## 13. Validation 명령

실제 fixture 이름은 구현 시 owning assembly와 기존 이름을 확인해 고정한다. 기본 순서는 다음과 같다.

```bash
./run_tests.sh full --filter EnemyAnimationBindingMigrationManifestTests
./run_tests.sh full --filter EnemyAnimationBindingMigrationDryRunTests
./run_tests.sh full --filter EnemyAnimationBindingMigrationSerializationTests
./run_tests.sh full --filter EnemyAnimationSparseBindingProductionContractTests
./run_tests.sh full --filter EnemyAnimationSparseBindingAssetCharacterizationTests
./run_tests.sh full --filter EnemyAnimationBindingEditorValidationTests
./run_tests.sh full --filter EnemyAnimationBindingDispatchTests
./run_tests.sh full --filter EnemyAnimationSparseBindingRuntimeScenarioTests

./run_tests.sh full --filter EnemyViewAnimatorControllerContractTests
./run_tests.sh full --filter GameplayTimingOwnershipTests
./run_tests.sh full --filter GameplayTickPresentationCoordinatorTests
./run_tests.sh full --filter EnemyViewPresentationMapperTests
./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests
./run_tests.sh full --filter GameplayVfxEnemyDeathMotionMigrationTests
./run_tests.sh full --filter UnitLocomotionPresentationAuthoringTests
./run_tests.sh full --filter EnemyPrefabScaffoldTests
./run_tests.sh full --filter EnemyViewPresentationAuthoringTests

./run_tests.sh core
```

UI runtime/asset 변경이 없으므로 `ui` lane은 기본 요구가 아니다. Broad unfiltered `full`도 기본 gate가 아니며, 실행하지 않았다면 그대로 기록한다.

## 14. Evidence 구조

새 evidence는 다음 아래에 둔다.

`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice2/<run-id>/`

권장 하위 구조:

- `00-preflight/`
  - revision, status, diff name/status
  - inventory count
  - production prefab hash
- `01-manifest/`
  - manifest validation result
  - raw/resolved baseline report
- `02-dry-run/`
  - semantic diff
  - no-mutation hash comparison
- `03-apply/`
  - per-prefab result
  - saved/reloaded identity comparison
- `04-targeted-tests/`
- `05-production-playmode/`
- `06-core/`
- `07-manual-editor/`
  - Inspector notes/screenshots
  - Missing Script/reimport result
- `08-final-diff/`
  - allowed/unexpected path audit
  - Controller/FBX/Scene zero-diff audit

Evidence에는 성공한 최종 run과 수정 과정의 실패 run을 구분한다. 실패 run을 삭제하지 않되 evidence 루트 전체를 green이라고 표현하지 않는다.

## 15. Hold 조건

다음 중 하나라도 발생하면 production apply 또는 후속 저장을 중단한다.

- Driver prefab baseline이 14가 아님
- production allowlist가 10이 아님
- 알려지지 않은 15번째 prefab 또는 Scene/`.asset` 직접 참조 발견
- manifest와 raw/resolved asset 값 불일치
- production prefab에 이미 알 수 없는 new binding이 존재
- JPeter Summon recovery에서 의미 있는 Animator dispatch 발견
- Astreton이 state Jump 대신 trigger를 실제 사용하거나 landing target이 다름
- assigned AOC effective clip 결과가 Slice 1 characterization과 다름
- runtime/Editor resolver가 같은 state를 다르게 판정
- Controller/FBX/AnimationClip/Scene 수정이 필요
- valid new binding에서 cue별 legacy fallback이 필요
- Kali/SecBot에 binding이 필요하다는 새 근거 발견
- prefab 저장 후 reload 결과 또는 GUID/fileID가 다름
- partial migration 상태를 자동 추론으로 보완해야 함
- production characterization, topology, DeathMotion, timing 또는 core 회귀
- 신규 fixture owning-stage matching count가 0
- 현재 사용자/Slice 1 diff와 안전하게 분리할 수 없음

## 16. Rollback

### Apply 전

- dry-run은 asset을 바꾸지 않으므로 별도 rollback이 없어야 한다.
- no-mutation fingerprint가 다르면 tool bug로 Hold한다.

### Apply 중 실패

- 이후 저장을 중단한다.
- 저장 완료/실패/미시도 prefab을 report한다.
- tool이 raw YAML 복원이나 자동 Git 명령을 실행하지 않는다.
- migration 전 checkpoint를 기준으로 사용자가 대상 파일 복구를 결정한다.

### 완료 commit 이후

Slice 2의 정식 rollback은 production migration commit 전체의 Git revert다. 다음을 함께 되돌려야 한다.

- 새 binding component
- Timing Authoring component 제거
- production contract test 전환
- manifest/tool의 migration 상태
- 관련 문서 상태

새 component만 수동 삭제하거나 Timing Authoring을 손으로 재작성하는 방식은 rollback으로 인정하지 않는다.

## 17. Commit 경계

Slice 2 apply와 검증이 완료되면 migration 결과는 하나의 원자적 intent로 묶는 것을 기본으로 한다.

권장 제목:

```text
refactor: Gameplay/EnemyAnimation - production View sparse binding 이관
```

본문에는 최소 다음을 기록한다.

- legacy flat authoring을 manifest 기반 cue binding으로 옮긴 이유
- production 8개 binding 추가와 Kali/SecBot no-binding disposition
- Timing Authoring 제거 및 Controller/clip identity 보존 방식
- migration 전후 contract/PlayMode/core evidence
- 전체 commit revert가 rollback 경로임

실제 commit 전에는 `commit-push-workflow` skill과 `AI_GIT_COMMIT_RULES.md`를 따른다.

## 18. Slice 2 완료 Gate

- manifest production row 10, 전체 disposition ledger 14
- `MigratedBinding == 8`, `ApprovedNoBinding == 2`
- production 8개의 root binding 정확히 1개 및 snapshot 유효
- Kali/SecBot binding 0
- production Timing Authoring component 0
- global Timing Authoring prefab reference 4개이며 모두 non-production allowlist에 속함
- Driver prefab count 14 유지
- production cue/mode/target/timing matrix 통과
- new binding의 legacy fallback 0
- same-tick Recovery timing + Hit dispatch 유지
- Astreton Jump state/landing/pending/topology 유지
- DrSaturn Utility exact cue/timing 유지
- JPeter Summon recovery counter-only/no-visual 유지
- Nebulous Glide state/timing 유지
- Death speed 1/duration 0 및 DeathMotion pose freeze 유지
- Controller/AOC/FBX/AnimationClip/Scene unexpected diff 0
- reload 후 Missing Script 0, GUID/fileID drift 0
- targeted tests와 core가 같은 최종 code/asset 상태에서 실패 0
- 모든 신규 fixture owning-stage matching count `> 0`
- evidence와 allowed/non-claim 문구 기록

이 Gate가 닫히기 전에는 Slice 3 비-production migration이나 Slice 4 legacy field 제거를 시작하지 않는다.
