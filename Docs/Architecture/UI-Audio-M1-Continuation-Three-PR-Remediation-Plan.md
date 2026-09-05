# UI/Audio M1 Continuation — 3-PR 분리 보정 계획

## 1. 목적과 상태

- 작성 기준일: 2026-09-05 KST
- 기준 main: `origin/main` `6a271d1b3058192de1eb4ca4e0c9027327203e9e`
- 원 작업 HEAD: `0649e82ad77b1649880ebf237266adf93ca4b81d`
- 상태: 보정 반영 및 재검토 완료, 구현 전 실행 계획

이 문서는 UI/Audio M1 continuation의 17개 선형 커밋을 다음 세 PR로 분리할 때 필요한
테스트 stratification 보정, SHA 보존 병합 순서, 검증 및 evidence 조건을 고정한다.
이 문서 자체는 브랜치 생성, merge, commit, push 또는 PR 생성을 승인하거나 수행하지 않는다.
이 문서, `UI-Audio-M1-Continuation-Three-PR-Execution-Prompt.md`와 `Docs/Architecture/README.md` index 변경은
PR A의 별도 docs 커밋에 귀속하며, PR A의 최종 검증은 해당 docs 커밋과 governance 보정이 모두 포함된
HEAD에서 수행한다. 아래 virtual merge tree 일치는 원 17개 커밋의 보정 전 production/content tree에 관한
사실이고, 이 두 문서와 보정 커밋은 승인된 추가 delta다.

| PR | 원 커밋 | 주제 |
|---|---|---|
| A | `397b2b995`~`3bf1adb62` | PassiveContactMinion asset ownership |
| B | `d0092688c`~`d7f0164bb` | EnemyView production presentation hardening |
| C | `2fb4a2d4e`~`0649e82ad` | sparse animation binding migration/retirement |

계약 분류는 다음과 같다.

- PR A의 profile별 mutable authoring asset 소유권은 `StrongContract`다.
- PR B/C의 presentation, Animator, VFX, Inspector 계약은 `StrongContract`다.
- presentation 경로는 `WorldState` 또는 `TickPipeline`의 authoritative state를 변경하지 않는다.
- fixture의 물리적 위치와 실행 Category는 별도 축이다. Category 보정으로 runtime 계약을 바꾸지 않는다.

## 2. 재검토 기준점

최신 main과 세 후보 트리를 alternate index로 구성해 strict checker를 독립 실행한 결과는 다음과 같다.

| 후보 | strict 오류 | 직전 후보 대비 신규 | 직전 후보 대비 해결 | 순증 |
|---|---:|---:|---:|---:|
| main | 2,124 | - | - | - |
| A | 2,125 | 1 | 0 | +1 |
| A+B | 2,136 | 19 | 8 | +11 |
| A+B+C | 2,217 | 82 | 1 | +81 |

main의 broad baseline이 이미 red이므로 raw exit code 또는 총 오류 수만으로 PR을 판정하지 않는다.
각 PR의 필수 governance gate는 직전 main과 후보의 정규화된 오류 집합을 비교했을 때
새 오류가 0개인 것이다. B의 해결 8건과 C의 해결 1건은 기존 테스트 삭제/rename에 따른 집합 변화이므로
신규 오류 수와 상계하지 않는다.

패치 적용성은 A, A+B, A+B+C 모두 최신 main에 적용 가능하고, A+B+C 재적용 tree는 최신 main과 원
브랜치의 virtual merge tree와 일치한다. A는 B/C와 파일 중첩이 없지만 B와 C는 22개 파일이 겹치므로
세 PR은 A, B, C 순서로 통합한다.

## 3. PR A 수정안

### 3.1 변경 파일

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyAiProfileAssetContractTests.cs`

### 3.2 정확한 수정

`PassiveContactMinion_Profile_OwnsDistinctMutableAuthoringGraph`의 primary category를 다음처럼 바꾼다.

```csharp
[Test]
[Category("Extended")]
public void PassiveContactMinion_Profile_OwnsDistinctMutableAuthoringGraph()
```

이 테스트는 `AssetDatabase` repository scan과 non-public reflection을 사용하므로 `Core`가 아니다.
장기적으로 Infrastructure/Full fixture로 분리할 수 있으나, 현재 fixture의 asset path 상수와 reflection helper를
복제하거나 public test support surface를 추가해야 한다. 3-PR 보정에서는 실행 티어만 `Extended`로 바로잡고
물리적 이동은 별도 구조 개선으로 남긴다.

이는 strict 신규 오류를 제거하기 위한 bounded deviation이며 전체 test stratification closure가 아니다.
물리 이동과 test-support 경계 재설계가 끝나기 전에는 이 PR이 placement debt까지 해소했다고 주장하지 않는다.

### 3.3 금지 변경

- PassiveContactMinion 및 Patroller production asset 값 변경
- profile compile 결과 또는 runtime AI 정책 변경
- 기존 unrelated `TutorialPassiveContact...` baseline category 정리

### 3.4 보정 커밋

```text
test: Gameplay/EnemyAI - asset contract category governance 정합화

- AssetDatabase와 reflection을 사용하는 ownership 검사가 Core로 분류되던 문제를 바로잡았다.
- profile ownership과 compiled tuning assertion은 바꾸지 않고 실행 티어만 Extended로 정렬했다.
- PR A가 최신 main 대비 새 strict governance 오류를 추가하지 않도록 했다.
```

## 4. PR B 수정안

### 4.1 변경 파일

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyViewAnimatorControllerContractTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyViewPresentationAuthoringTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayPresentationOrchestrationArchitectureTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTickPresentationCoordinatorTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayVfxParameterizedMotionRuntimeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/SummonedEnemyPresentationResolverTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/GameplayTestStratificationOverrides.json`
- `Tools/gameplay_test_stratification_lib.py`

production C#, Prefab, Animator Controller, AnimationClip과 ScriptableObject는 이 보정 커밋에서 변경하지 않는다.
`EnemyViewPresentationAuthoringTests.cs`는 원 B 범위와 category 의도를 확인하는 대상이지만, 보정 source diff는
필요하지 않다. 해당 메서드는 이미 `Full`이고 override JSON만 추가한다.

### 4.2 Controller fixture 7개

`EnemyViewAnimatorControllerContractTests`의 fixture-level `[Category("Full")]`을 제거한다. NUnit의 상속
category와 method category가 동시에 적용되는 모호성을 없애기 위해 7개 테스트 메서드 각각에
`[Category("Full")]`을 둔다.

이 fixture 전체가 production prefab/controller asset contract이므로 `FULL_FILE_NAMES`에 다음 항목을 추가한다.

```python
"EnemyViewAnimatorControllerContractTests.cs",
```

개별 FQN 6개를 override로 나열하지 않는다. 파일 전체의 Full 의도가 바뀌면 한 곳에서 함께 검토되게 한다.
Controller fixture를 Unit에 유지하는 것도 이번 strict 보정의 bounded deviation이다. Infrastructure로 옮기려면
Stages, VFX Host 및 TestInfrastructure reference 경계를 함께 다시 설계해야 하므로 별도 구조 개선으로 남기며,
이번 PR에서 full stratification closure를 주장하지 않는다.

### 4.3 나머지 12개 오류

다음 신규 메서드는 method-level category를 `Extended`로 바꾼다.

| Fixture | 수 | 변경 |
|---|---:|---|
| `GameplayPresentationOrchestrationArchitectureTests` | 1 | `Core` -> `Extended` |
| `GameplayTickPresentationCoordinatorTests` | 2 | `Core` -> `Extended` |
| `GameplayVfxParameterizedMotionRuntimeTests` | 7 | `Core` -> `Extended` |

`SummonedEnemyPresentationResolverTests`의 신규 retry/teardown 메서드 1개는 broad retry 경로이므로
`Extended`에서 `Full`로 바꾼다.

`EnemyViewPresentationAuthoringTests.Assets_DoNotRetainRemovedEntityEffectPresentationAuthoringScriptOrGuid`는
repository asset residue scan이므로 `Full`을 유지한다. 이 파일 전체를 Full로 올리지 않고 해당 FQN 하나만
`GameplayTestStratificationOverrides.json`에 `category: Full`, `locked: true`, 구체적인 asset-wide scan 이유와 함께
등록한다.

### 4.4 보정 커밋

```text
test: Gameplay/EnemyView - presentation test tier governance 정합화

- production Controller fixture의 Full 의도를 메서드별 category와 파일 정책으로 명시했다.
- architecture, coordinator, VFX 및 resolver 테스트를 실제 실행 비용과 범위에 맞게 재분류했다.
- asset-wide residue 검사는 좁은 locked override로 유지해 일반 Unit 테스트를 Full로 확장하지 않았다.
```

## 5. PR C 수정안

PR C 보정은 테스트 category 명시와 governance 경계 정합화를 별도 커밋으로 나눈다.

### 5.1 메서드별 category

fixture-level primary category를 제거하고 다음 모든 NUnit 테스트 메서드에 category를 직접 둔다.

| 위치/fixture | 실제 대상 | category |
|---|---:|---|
| `EditMode/Core/EnemyAnimationBindingSnapshotTests.cs` | 10 | `Core` |
| `EditMode/Core/EnemyAnimationCueCatalogTests.cs` | 3 | `Core` |
| `EditMode/Scenario/EnemyAnimationBindingDispatchTests.cs` | 15 | `Full` |
| `EditMode/Scenario/EnemyAnimationSparseBindingRuntimeScenarioTests.cs` | 19 | `Full` |
| `EditMode/TestSupport/Infrastructure/EnemyAnimationBindingAuthoringTests.cs` | 6 | `Full` |
| `EditMode/TestSupport/Infrastructure/EnemyAnimationBindingEditorValidationTests.cs` | 13 | `Full` |
| `EditMode/TestSupport/Infrastructure/EnemyAnimationBindingMigrationTests.cs` | 13 | `Full` |
| `EditMode/TestSupport/Infrastructure/EnemyAnimationSparseBindingAssetCharacterizationTests.cs` | 6 | `Full` |
| `EnemyViewAnimatorControllerContractTests.cs`에서 C로 신규/rename된 FQN | 2 | `Full` |

총 대상은 Core 13개, Scenario 34개, Infrastructure 38개, Controller 2개로 87개 test method다.
이는 XML의 expanded test-case 실행 수가 아니라 primary category를 부착할 C# 메서드 수다. 현재 checker가
보고한 category 오류는 79개이며, discovery가 놓친 `[TestCase]`/`[TestCaseSource]` 전용 메서드 8개도 포함한다.
Controller 2개는 A+B 대비 신규 1개와 rename 1개이며, 보정 후 최종 Controller fixture의 8개 메서드 모두가
method-level `Full`을 가져야 한다.

C에서 제거할 fixture-level primary category는 8개 파일의 총 10개다. 특히
`EnemyAnimationBindingMigrationTests.cs`에는 fixture가 세 개 있으므로 각 fixture의 category를 모두 제거한다.

다음 여섯 파일은 canonical 구현 계획이 fixture 전체를 Full로 분류하므로 `FULL_FILE_NAMES`에 등록한다.

```python
"EnemyAnimationBindingDispatchTests.cs",
"EnemyAnimationSparseBindingRuntimeScenarioTests.cs",
"EnemyAnimationBindingAuthoringTests.cs",
"EnemyAnimationBindingEditorValidationTests.cs",
"EnemyAnimationBindingMigrationTests.cs",
"EnemyAnimationSparseBindingAssetCharacterizationTests.cs",
```

fixture 전체가 같은 실행 티어이므로 60개 이상의 FQN override는 만들지 않는다. 일부 메서드만 다른 티어가
되어야 할 때만 locked override를 사용한다.

### 5.2 Core Host import 경계

다음 두 Core fixture는 `Game.Feature.Gameplay.Host` namespace를 import한다.

- `EnemyAnimationBindingSnapshotTests.cs`
- `EnemyAnimationCueCatalogTests.cs`

검증 대상 타입은 실제로 `Game.Feature.Gameplay` root assembly에 컴파일되고, 테스트는 runtime pipeline을
실행하지 않는 deterministic catalog/snapshot 계약이다. canonical 구현 계획도 이 두 fixture를 Core로 고정한다.

`FORBIDDEN_CORE_IMPORTS`에서 Host 전역 금지를 제거하지 않는다. 대신 다음 형태의 exact-path 예외를 추가한다.

```python
CORE_IMPORT_EXCEPTIONS_BY_PATH = {
    "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Core/EnemyAnimationBindingSnapshotTests.cs": {
        "Game.Feature.Gameplay.Host",
    },
    "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Core/EnemyAnimationCueCatalogTests.cs": {
        "Game.Feature.Gameplay.Host",
    },
}
```

하나의 helper가 path와 import를 함께 판정하게 하고 `is_core_candidate`와 `check_core_assembly_rules`가 이를
공유한다. helper 입력은 `TestMethod.relative_path`와 `source_path.relative_to(root).as_posix()`로 정규화한다.
비예외 Core 파일의 exact `using Game.Feature.Gameplay.Host;`는 계속 실패하고 execution/composition symbol
규칙은 별도로 유지된다. 현재 source 검사는 alias와 fully-qualified Host 사용을 모두 탐지하지 않으므로
임의의 다른 Host symbol까지 차단한다고 주장하지 않는다.
animation contract namespace를 Host 밖으로 옮기는 작업은 production namespace/API 영향이 있으므로 별도 PR이다.

### 5.3 Infrastructure Editor assembly 경계

`Game.TestInfrastructure.asmdef`은 Inspector, migration manifest, controller validator를 직접 검증하기 위해
`Game.Feature.Gameplay.EnemyPresentation.Editor`를 참조한다. 기존 asmdef와
`InternalsVisibleTo("Game.TestInfrastructure")`는 유지한다.

`check_infrastructure_rules`의 허용 집합에 다음 exact assembly 하나를 추가한다.

```python
APPROVED_INFRASTRUCTURE_REFERENCES = {
    "Game.Feature.Gameplay",
    "Game.Feature.Gameplay.Tests",
    "Game.Feature.Gameplay.EnemyPresentation.Editor",
}
```

UnityEditor 전체, 임의 Editor assembly, runtime host assembly를 허용하지 않는다. 전용 Editor test asmdef는
관련 세 fixture, IVT, assembly counting 및 path governance를 함께 이동해야 하므로 이번 최소 보정에는 넣지 않는다.
이 예외는 `Game.TestInfrastructure` assembly 전체에 compile reference를 제공한다. 세 fixture만의 소비를 assembly가
강제하는 것은 아니므로, exact `Game.Feature.Gameplay.Host.EditorTools` import path 검사를 추가하거나 이 제약을
잔여 구조 리스크로 PR에 기록한다.

### 5.4 Governance boundary test

C의 governance 보정 커밋에는 `Tools/test_gameplay_test_stratification_lib.py`라는 작은 `unittest` 기반
boundary test를 추가하고 `python3 Tools/test_gameplay_test_stratification_lib.py`로 실행한다. 이 test는
다음을 직접 검증한다.

- 위 두 exact Core path와 Host import 조합만 승인되고 third/near-miss path는 거부됨
- exact EnemyPresentation Editor assembly만 Infrastructure에서 승인되고 임의 Editor/runtime Host는 거부됨
- B의 Controller 파일과 C의 여섯 파일, 총 일곱 `FULL_FILE_NAMES`가 모두 `Full`로 계산됨
- repo-relative path separator 정규화가 Windows/WSL 입력에서 동일하게 동작함

TestCase discovery parser 수정과 그 회귀 테스트는 이 boundary test에 섞지 않고 별도 governance PR에 둔다.

### 5.5 보정 커밋

```text
test: Gameplay/EnemyAnimation - sparse binding 테스트 실행 티어 명시

- Core, Scenario, Infrastructure 및 Controller 테스트 87개의 primary category를 메서드에 직접 명시했다.
- asset, Inspector, migration 및 runtime scenario fixture의 canonical Full 실행 의도를 유지했다.
- TestCase-only 메서드도 현재 discovery 누락과 무관하게 동일한 실행 티어를 갖도록 했다.
```

```text
chore: Project - animation test governance 경계 정합화

- sparse animation의 파일 단위 Full 정책을 source-truth checker에 반영했다.
- 두 deterministic Core fixture와 EnemyPresentation Editor assembly만 exact exception으로 승인했다.
- 기존 Core Host 금지와 Infrastructure reference 차단은 나머지 파일과 assembly에 그대로 유지했다.
```

## 6. SHA 보존 브랜치와 병합 절차

원 커밋 SHA를 main ancestry에 남기기 위해 cherry-pick, rebase merge, squash merge를 사용하지 않는다.

### 6.1 작업 시작 전 조건

1. `gh` CLI와 인증 또는 같은 repository/PR/CI/review/merge API를 제공하는 인증된 GitHub connector 중
   하나를 먼저 확인한다. 둘 다 없으면 branch/worktree 생성 전에 중단하며 작업 중 설치를 임의로 시도하지 않는다.
2. GitHub repository 설정 또는 PR merge menu에서 `Create a merge commit`이 현재 허용되는지 먼저 확인한다.
3. 현재 plan/execution prompt/README 변경을 PR A docs commit 후보로 안전하게 보존하기 전에는 이
   worktree에서 switch/merge하지 않는다.
4. 원격보다 한 커밋 앞선 로컬 `0649e82ad77b1649880ebf237266adf93ca4b81d`를 명시적 local backup ref로
   고정하고 `git show-ref`로 확인한다.
5. 로컬 저장소 밖의 내구성 있는 보호가 필요하다. 새 원격 상태 변경은 사용자 승인 후 backup branch로 push하고,
   그 전에는 `/mnt/d/J2M/evidence` 아래 `git bundle`과 SHA manifest로 보호한다.
6. `j2m-worktree-audit`을 실행하고 D 드라이브 여유 공간 30 GiB 이상을 확인한다. 새 Unity worktree는
   `j2m-worktree-add`만 사용하며, Unity 검증 전에 resolved project path가 `/mnt/d/J2M/worktrees/`로 시작하는지 확인한다.

merge commit이 비활성화되어 있으면 작업을 시작하지 않는다. squash는 Slice 4A 부분 rollback 경계를
붕괴시키며, rebase는 문서의 모든 원 SHA를 바꾼다. 불가피하게 squash/rebase를 사용하려면 old-to-new SHA 표,
다섯 개 provenance 문서의 checkpoint/rollback 수정, 전 evidence 재실행을 별도 승인 범위로 잡는다.

### 6.2 PR별 endpoint 통합

각 PR은 `git fetch origin main` 후 `PR_BASE_SHA=$(git rev-parse origin/main)`을 고정하고 그 SHA에서 branch를
만든다. endpoint merge는 즉시 commit하지 않고 staged 상태를 먼저 검토한다.

`PR_BASE_SHA`, `PR_HEAD_SHA`, `PR_HEAD_TREE`, endpoint, branch/worktree path, PR number와 evidence root는
PR별 `/mnt/d/J2M/evidence/ui-audio-m1-continuation/<pr-id>/state.env`에 기록한다. 별도 shell/exec 호출마다
`set -euo pipefail` 뒤 exact state file을 source하고 repository/branch/resolved path/HEAD/tree를 assert한다.
shell 변수의 호출 간 지속을 가정하지 않는다. 최초 state 초기화도 새 worktree의 한 exec에서 repository,
base/head/tree, endpoint, branch/path, body file/hash를 모두 다시 계산하고 temporary file과 atomic rename으로
수행한다. commit, base drift 또는 PR 생성으로 값이 바뀌면 state file을
원자 갱신한다. final validation, PR 생성, ready 확인, merge 직전, merge 결과의 phase별 immutable snapshot과
SHA-256을 남기며, 모든 artifact가 같은 repository/base/head/tree/branch/worktree/evidence root를 가리키는지
assert한다. head 또는 base가 바뀌면 새 evidence root에서 validation부터 다시 수행한다.

```bash
set -euo pipefail
git merge --no-ff --no-commit "<FULL_ENDPOINT_SHA>"
git diff --cached --stat
git diff --cached --name-status
git diff --cached --check -- '*.cs' '*.py' '*.json' '*.md' '*.asmdef' '*.sh'
```

Unity serialized YAML은 staged name-status와 semantic/manual 검증으로 별도 확인한다.

순서는 다음과 같다.

1. PR A: `3bf1adb62e4f98d3e54c8f764744a990038acdf3` endpoint를 통합하고, docs commit과 A 보정을 추가한다.
2. PR A의 final HEAD를 검증하고 GitHub `Create a merge commit`으로 병합한다.
3. 새 main을 fetch한 뒤 PR B: `d7f0164bb74108d5ca50aacd9c1b50feb25d4d42` endpoint를 통합하고 B 보정을 추가한다.
4. PR B의 final HEAD를 검증하고 같은 방식으로 병합한다.
5. 새 main을 fetch한 뒤 PR C: `0649e82ad77b1649880ebf237266adf93ca4b81d` endpoint를 통합하고 C 보정을 추가한다.
6. PR C의 final HEAD를 검증하고 같은 방식으로 병합한다.

각 endpoint 통합과 보정 후에는 고정 base 기준 diff와 commit 목록을 검사한다. PR scope는 원 commit range에
문서가 승인한 remediation path만 더한 범위다.

- A: 원 17개 파일, plan/execution prompt/README docs, 기존 A test 파일의 category 보정
- B: 원 52개 파일과 `GameplayTestStratificationOverrides.json`, `Tools/gameplay_test_stratification_lib.py`
- C: 원 85개 파일과 `Tools/gameplay_test_stratification_lib.py`, governance boundary test

B와 최신 main은 presentation production/test 6개 파일, C와 최신 main은 coordinator test와 README가 겹친다.
텍스트 conflict가 없더라도 CameraShake의 `sourceTickIndex`, completed-motion cleanup 및 이 plan의 README link
보존을 확인한다.

### 6.3 Base drift와 병합 결과

검증 직전에 최신 main을 별도 변수로 읽고, branch 생성 또는 직전 재검증 때 고정한 `PR_BASE_SHA`와 비교한다.

```bash
git fetch origin main
CURRENT_MAIN_SHA=$(git rev-parse origin/main)
```

`CURRENT_MAIN_SHA`가 검증에 사용한 `PR_BASE_SHA`와 다르면 rebase하지 않는다. 새 main을 PR branch에 merge하고
conflict/semantic diff를 다시 검토한다. merge commit이 끝난 뒤에만 `PR_BASE_SHA="$CURRENT_MAIN_SHA"`로
갱신하고 모든 governance, functional 및 manual 검증을 재실행한다. base가 확정된 final head에서 다음을
evidence에 기록한다.

```bash
PR_HEAD_SHA=$(git rev-parse HEAD)
PR_HEAD_TREE=$(git rev-parse 'HEAD^{tree}')
```

병합 후 main을 다시 fetch하고 GitHub가 기록한 실제 PR merge commit을 기준으로 merge parent, PR HEAD
ancestry, endpoint ancestry와 tree를 확인한다. `origin/main`에 후속 commit이 생겨도 그 tip을 PR merge
commit으로 오인하지 않는다.

```bash
git fetch origin main
MAIN_SHA=$(git rev-parse origin/main)
PR_MERGE_SHA=$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json state,mergedAt,mergeCommit --jq '.mergeCommit.oid')
test -n "$PR_MERGE_SHA"
git merge-base --is-ancestor "$PR_MERGE_SHA" "$MAIN_SHA"
read -r FIRST_PARENT SECOND_PARENT EXTRA_PARENT <<< "$(git show -s --format='%P' "$PR_MERGE_SHA")"
test -n "$FIRST_PARENT" && test -n "$SECOND_PARENT" && test -z "$EXTRA_PARENT"
test "$FIRST_PARENT" = "$PR_BASE_SHA"
test "$SECOND_PARENT" = "$PR_HEAD_SHA"
git merge-base --is-ancestor "$PR_HEAD_SHA" "$PR_MERGE_SHA"
git merge-base --is-ancestor "<FULL_ENDPOINT_SHA>" "$PR_MERGE_SHA"
test "$(git rev-parse "${PR_MERGE_SHA}^{tree}")" = "$PR_HEAD_TREE"
```

tree가 다르면 PR HEAD evidence를 병합 결과에 귀속하지 않고 merge tree에서 영향 범위에 비례해 재검증한다.

merge는 각 PR의 exact repository/number/head를 지목한 별도 사용자 승인 뒤에만 즉시 merge API로 수행한다.
API rc/stdout/stderr가 timeout 또는 응답 유실을 나타내면 같은 승인을 사용해 merge API를 재호출하지 않는다.
read-only PR state/mergeCommit 조회에서 이미 `MERGED`이고 exact head의 merge SHA가 확인될 때만 parent/tree
검증으로 복구하며, 그 외에는 phase attempt evidence를 보존하고 중단한다.

endpoint ancestor 검사는 매 단계 새 main을 fetch한 뒤 누적해서 수행한다.

- A 병합 후: `3bf1adb62e4f98d3e54c8f764744a990038acdf3`
- B 병합 후: A endpoint와 `d7f0164bb74108d5ca50aacd9c1b50feb25d4d42`
- C 병합 후: 위 둘과 `0649e82ad77b1649880ebf237266adf93ca4b81d`

## 7. 검증과 evidence

모든 검증은 해당 PR 최종 HEAD worktree에서 실행하고 새 evidence는 `/mnt/d/J2M/evidence` 아래 PR별 디렉터리에
보존한다. 과거 Slice evidence는 historical comparison으로만 사용한다.

### 7.1 공통 gate

각 PR branch를 만들 때 고정한 `PR_BASE_SHA`를 사용해 committed PR 범위를 검사한다. clean HEAD의
working-tree diff만 검사하는 단독 `git diff --check`는 evidence로 사용하지 않는다.

```bash
git diff --stat "$PR_BASE_SHA"...HEAD
git diff --name-status "$PR_BASE_SHA"...HEAD
git diff --check "$PR_BASE_SHA"...HEAD -- '*.cs' '*.py' '*.json' '*.md' '*.asmdef' '*.sh'
git log --no-merges --oneline "$PR_BASE_SHA"..HEAD
git log --first-parent --format='%H%x09%P%n%B%n---' "$PR_BASE_SHA"..HEAD
```

first-parent log에서 endpoint merge와 각 remediation/docs commit의 순서, 제목, 전체 body와 최소 두 bullet을
검토한다. A는 endpoint -> remediation -> docs, B는 endpoint -> remediation, C는 endpoint -> category ->
governance 순서이며 base drift sync merge는 필요할 때만 그 뒤에 별도 commit으로 존재해야 한다.

Unity serialized YAML의 blank scalar whitespace는 이 hard gate에 포함하지 않는다. `.prefab`, `.asset`, `.unity`,
`.controller` 등은 변경 목록을 따로 기록하고 YAML residue, GUID/fileID, reimport 및 manual/editor 검증으로 판정한다.

strict와 source/inventory delta는 `TestResults/.governance` history가 없는 두 archive root에서 같은 조건으로
실행한다. 예시는 다음과 같다.

```bash
set -euo pipefail
BASE_AUDIT_ROOT=$(mktemp -d)
HEAD_AUDIT_ROOT=$(mktemp -d)
AUDIT_EVIDENCE_ROOT="$PR_EVIDENCE_ROOT/governance"
mkdir -p "$AUDIT_EVIDENCE_ROOT"
git archive "$PR_BASE_SHA" Tools Assets/_Features/Gameplay/Gameplay_Tests | tar -x -C "$BASE_AUDIT_ROOT"
git archive HEAD Tools Assets/_Features/Gameplay/Gameplay_Tests | tar -x -C "$HEAD_AUDIT_ROOT"
set +e
python3 "$BASE_AUDIT_ROOT/Tools/check_gameplay_test_stratification.py" --root "$BASE_AUDIT_ROOT" --mode strict >"$AUDIT_EVIDENCE_ROOT/base-strict.log" 2>&1
BASE_STRICT_RC=$?
python3 "$HEAD_AUDIT_ROOT/Tools/check_gameplay_test_stratification.py" --root "$HEAD_AUDIT_ROOT" --mode strict >"$AUDIT_EVIDENCE_ROOT/head-strict.log" 2>&1
HEAD_STRICT_RC=$?
python3 "$BASE_AUDIT_ROOT/Tools/generate_gameplay_test_stratification.py" --root "$BASE_AUDIT_ROOT" --check >"$AUDIT_EVIDENCE_ROOT/base-generate.log" 2>&1
BASE_GENERATE_RC=$?
python3 "$HEAD_AUDIT_ROOT/Tools/generate_gameplay_test_stratification.py" --root "$HEAD_AUDIT_ROOT" --check >"$AUDIT_EVIDENCE_ROOT/head-generate.log" 2>&1
HEAD_GENERATE_RC=$?
set -e
printf 'base_strict=%s\nhead_strict=%s\nbase_generate=%s\nhead_generate=%s\n' \
    "$BASE_STRICT_RC" "$HEAD_STRICT_RC" "$BASE_GENERATE_RC" "$HEAD_GENERATE_RC" \
    >"$AUDIT_EVIDENCE_ROOT/exit-codes.txt"

normalize_strict() {
    awk '/^(ERROR|WARNING):/ {
        sub(/^(ERROR|WARNING):[[:space:]]*/, "")
        if ($0 !~ /^Core candidate debt is above threshold:/) print
    }' "$1" | LC_ALL=C sort -u
}
normalize_generate() {
    if [ "$1" -eq 0 ]; then
        return 0
    fi
    awk 'NF { print }' "$2" | LC_ALL=C sort -u
}
normalize_strict "$AUDIT_EVIDENCE_ROOT/base-strict.log" >"$AUDIT_EVIDENCE_ROOT/base-strict.normalized"
normalize_strict "$AUDIT_EVIDENCE_ROOT/head-strict.log" >"$AUDIT_EVIDENCE_ROOT/head-strict.normalized"
normalize_generate "$BASE_GENERATE_RC" "$AUDIT_EVIDENCE_ROOT/base-generate.log" >"$AUDIT_EVIDENCE_ROOT/base-generate.normalized"
normalize_generate "$HEAD_GENERATE_RC" "$AUDIT_EVIDENCE_ROOT/head-generate.log" >"$AUDIT_EVIDENCE_ROOT/head-generate.normalized"
comm -13 "$AUDIT_EVIDENCE_ROOT/base-strict.normalized" "$AUDIT_EVIDENCE_ROOT/head-strict.normalized" >"$AUDIT_EVIDENCE_ROOT/head-only-strict.txt"
comm -13 "$AUDIT_EVIDENCE_ROOT/base-generate.normalized" "$AUDIT_EVIDENCE_ROOT/head-generate.normalized" >"$AUDIT_EVIDENCE_ROOT/head-only-generate.txt"
test ! -s "$AUDIT_EVIDENCE_ROOT/head-only-strict.txt"
test ! -s "$AUDIT_EVIDENCE_ROOT/head-only-generate.txt"

BASE_CANDIDATE_COUNT=$(sed -n 's/^Core candidate count: \([0-9][0-9]*\).*/\1/p' "$AUDIT_EVIDENCE_ROOT/base-strict.log")
HEAD_CANDIDATE_COUNT=$(sed -n 's/^Core candidate count: \([0-9][0-9]*\).*/\1/p' "$AUDIT_EVIDENCE_ROOT/head-strict.log")
test -n "$BASE_CANDIDATE_COUNT" && test -n "$HEAD_CANDIDATE_COUNT"
test "$HEAD_CANDIDATE_COUNT" -le "$BASE_CANDIDATE_COUNT"
printf 'base=%s\nhead=%s\n' "$BASE_CANDIDATE_COUNT" "$HEAD_CANDIDATE_COUNT" >"$AUDIT_EVIDENCE_ROOT/candidate-counts.txt"
```

각 command의 stdout/stderr와 exit code를 보존한다. strict는 `ERROR:`/`WARNING:` diagnostic만 추출하고
prefix와 동적인 candidate-debt warning을 제거한다. generator는 exit 0이면 diagnostic 공집합, nonzero이면
non-empty stderr 행 집합으로 정규화한다. summary 수치 전체를 오류 집합에 섞지 않는다. 두 정규화 집합에서
각각 head-only 행 0을 요구하고 candidate count는 별도 숫자로 비교해 비증가를 확인한다. raw nonzero는 기존
baseline 때문에 단독 실패 판정으로 쓰지 않고 base/head 신규 delta를 gate로 사용한다.

functional lane은 strict delta gate와 분리해 실행한다. PR별 최종 HEAD를 포함하는 고유 경로를 사용한다.
각 D worktree에서 먼저 `./run_tests.sh --print-config`와 `./run_tests.sh --dry-run core`를 실행해
`PROJECT_PATH_WSL`/`PROJECT_PATH_WIN`이 현재 worktree를 가리키는지 기록한다. 이는 path preflight이며 test
통과 evidence로 계산하지 않는다.

```bash
export PR_EVIDENCE_ROOT="/mnt/d/J2M/evidence/ui-audio-m1-continuation/<pr-id>/<full-head-sha>"
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/core" ./run_tests.sh core
```

soft mode는 별도 archive strict delta에서 신규 오류 0을 먼저 확인했을 때만 허용한다. 실행 전후 HEAD SHA,
tree SHA와 clean status를 evidence에 기록하며 이후 tracked 변경이 생기면 해당 HEAD 검증을 다시 실행한다.
`run_tests.sh`의 결과 파일명은 lane별로 고정되므로 위 PR root 바로 아래를 재사용하지 않고, 아래 PR별 명령처럼
각 core/filtered invocation에 고유한 하위 `CODEX_VALIDATION_ROOT`를 사용해 XML/log 덮어쓰기를 막는다.

### 7.2 PR A

```bash
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-enemy-ai-profile" ./run_tests.sh full --filter EnemyAiProfileAssetContractTests
```

- ScriptableObject별 owner path와 exclusive mutable graph 확인
- asset과 `.meta` pairing 및 GUID 보존 확인
- compiled tuning parity 확인

### 7.3 PR B

```bash
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-presentation-contracts-primary" ./run_tests.sh full --filter EnemyViewAnimatorControllerContractTests,EnemyViewPresentationAuthoringTests,GameplayPresentationOrchestrationArchitectureTests,GameplayTickPresentationCoordinatorTests,GameplayVfxParameterizedMotionRuntimeTests,SummonedEnemyPresentationResolverTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-presentation-contracts-secondary" ./run_tests.sh full --filter EnemyAudioRuntimeTests,FlipInteractionPlannerInternalTests,GameplayTimingOwnershipTests,GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests,GameplayVfxPlayerDamageMigrationTests,GameplayViewProjectionTests,PresentationPoseArbitrationTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-animator-playmode" ./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests
```

- production prefab/controller contract 및 actual Animator PlayMode 확인
- latest main의 CameraShake 변경 보존 확인
- Prefab 목적과 manual/editor 검증 기록

### 7.4 PR C

```bash
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-binding-infrastructure" ./run_tests.sh full --filter EnemyAnimationBindingSnapshotTests,EnemyAnimationCueCatalogTests,EnemyAnimationBindingAuthoringTests,EnemyAnimationBindingEditorValidationTests,EnemyAnimationBindingMigrationManifestTests,EnemyAnimationSparseBindingProductionContractTests,EnemyAnimationBindingMigrationAssetCharacterizationTests,EnemyAnimationSparseBindingAssetCharacterizationTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-binding-runtime" ./run_tests.sh full --filter EnemyAnimationBindingDispatchTests,EnemyAnimationSparseBindingRuntimeScenarioTests,GameplayTimingOwnershipTests,EnemyViewAnimatorControllerContractTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-inherited-touched" ./run_tests.sh full --filter EnemyPrefabScaffoldTests,EnemyViewPresentationAuthoringTests,GameplayTickPresentationCoordinatorTests,GameplayViewProjectionTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-animation-playmode" ./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests,EnemyPresentationReadinessPlayModeTests
```

- production 10 View의 sparse binding/controller/clip identity 확인
- 새 PR HEAD에서 Kali(`animator` only, Binding/Timing 없음), Startis(Hit/Death),
  Astreton(Jump 3 State + ActionExecute/Hit/Death + crossfade `0.001`),
  DrSaturn(Utility windup/recovery timing + Hit/Death), Nebulous(Glide 3 State + timing/reference + crossfade `0`)
  exact 5-View Inspector matrix와 PNG 확인
- Missing Script 0과 retired YAML/GUID residue 확인
- capture 전후 tracked diff 0과 HEAD/tree/fingerprint manifest 확인
- Inspector 검증 중 Prefab, Controller, clip을 저장하거나 변경하지 않았음을 확인

UI source/asset 변경이 없으므로 `./run_tests.sh ui`는 실행하지 않고 그 이유를 PR evidence에 기록한다.
broad unfiltered `full`을 실행하지 않거나 baseline red로 중단되면 touched-cluster 결과와 분리해 보고하고
project-wide 또는 full-lane green을 주장하지 않는다.

## 8. PR 설명 필수 항목

각 PR 설명은 다음을 포함한다.

- base branch, head branch, 최종 head SHA와 tree SHA
- 포함한 원 커밋 range와 추가 governance 보정 커밋
- PR A에 귀속한 plan/execution prompt/README docs commit
- production/Prefab/ScriptableObject/asset 변경 목적
- 실행한 명령, 결과, XML/log/evidence 경로
- 실행하지 않은 `ui`, broad `full`, manual lane과 사유
- 직전 main 대비 strict 신규 오류 0 여부
- 기존 baseline 실패와 touched-cluster 결과의 분리
- merge commit 방식 요구와 rollback 단위
- final head의 CI 상태, mergeability, actionable unresolved review 상태

PR C는 기존 sparse-binding evidence가 원 SHA의 historical evidence임을 명시하고, 새 PR HEAD evidence를
별도로 연결한다. 전체 stack rollback은 GitHub merge commit을 C, B, A 역순으로 되돌린다.

```text
PR C merge revert -> PR B merge revert -> PR A merge revert
```

각 merge SHA의 첫 parent가 당시 main인지 `git show -s --format='%H %P'`로 확인한 뒤에만
`git revert -m 1 <merge-sha>`를 사용한다. A 또는 B부터 단독으로 먼저 되돌려 후속 stacked contract가 남는
상태를 만들지 않는다.

실제 rollback은 별도 사용자 승인 후 최신 main에서 `j2m-worktree-add`로 만든 D 드라이브 rollback 전용
worktree/branch에서만 수행한다. legacy worktree나 `main`에 직접 쓰거나 push하지 않는다. 전체 rollback은
C, B, A full merge SHA를 각각 `git revert --no-commit -m 1`로 역순 적용하고 staged 결과를 검토한 뒤 규칙에
맞는 body 포함 commit으로 만든다. 같은 revision에서 strict/core/touched 검증을 수행하고 non-force push한
별도 rollback PR로 제출한다.

Slice 4A 부분 rollback은 C category/governance 보정 커밋 전체를 먼저 revert하지 않는다. 먼저 관련 production
후속 변경을 최신순으로 `--no-commit` revert하고, `EnemyAnimationBindingMigrationTests.cs` 등 충돌 파일에서는
살아남는 테스트의 method category와 공통 governance 정책을 유지한다. 그 다음
`115602d35713c94647c624b107d22e5f0836be2c`, `df82bea239d04300d011c8118e0ba2924f016bd2`
순서로 revert하고 strict delta, migration/residue fixture 및 `core`를 다시 실행한다. 실제 보정 SHA와 유지한
hunk는 PR C rollback ledger에 기록한다. 이 절차를 검증하지 않은 상태에서는 기존 Slice 4A 부분 rollback이
자동 보존된다고 주장하지 않는다.

## 9. 별도 후속 governance 문제

`Tools/gameplay_test_stratification_lib.py::discover_tests`는 single-line attribute block에 exact `[Test]` 또는
`[UnityTest]`가 있고 public signature가 같은 줄에서 끝나는 좁은 형식만 발견한다. 그 결과
`[TestCase]`/`[TestCaseSource]`만 가진 메서드, attribute와 signature 사이에 주석이 있는 메서드 및 multiline
attribute/signature를 누락할 수 있다. PR C의 category 대상 87개 중 다음 8개가 현재 79개 오류 집계에
포함되지 않았다.

- `EnemyAnimationBindingSnapshotTests.Create_RejectsUnknownOrNoneCue`
- `EnemyAnimationBindingSnapshotTests.Create_RejectsInvalidTimingDuration`
- `EnemyAnimationBindingSnapshotTests.Create_RejectsInvalidCrossFade`
- `EnemyAnimationCueCatalogTests.Catalog_DefinesAllowedDispatchAndTiming`
- `EnemyAnimationBindingDispatchTests.NoBindingWithoutTiming_AllCuesAreUnsupportedAndNeverQueue`
- `EnemyAnimationBindingEditorValidationTests.InspectorHelpBox_UnsupportedAuthoring_PreservesRuntimeValidationMessage`
- `EnemyAnimationBindingMigrationManifestTests.ManifestAndLedgerAudit_RejectsEachLiveIdentityAxis`
- `EnemyAnimationBindingMigrationManifestTests.DriverYamlAudit_UsesEveryDocumentHeaderAsBoundary_AndSupportsFinalEof`

최종 A+B+C touched set에는 B가 추가하고 C가 rename한
`EnemyViewPresentationAuthoringTests.EnemyPrefab_SparseBindingAuthorsDeathWithoutTiming`도 포함되어 최소 9개가
discovery 밖에 있다. 이 메서드는 source category가 이미 `Full`이라 B의 19개 category 보정 수에는 포함되지 않는다.

이 문제는 기존 저장소 전체 inventory와 baseline을 재분류할 수 있으므로 A/B/C에 섞지 않고 별도 governance
PR로 처리한다. 후속 PR은 다음을 포함한다.

- `Test`, `UnityTest`, `TestCase`, `TestCaseSource`를 test marker로 인식하는 balanced attribute/signature parser
- single-line/multiline attribute, attribute와 signature 사이 주석, multiline method signature 및 복수
  `TestCase`에 대한 parser 회귀 테스트
- 변경 전후 전체 discovered FQN 목록과 category/error delta
- 새로 드러난 기존 baseline과 parser 자체 회귀의 분리

현재 Gameplay test root에서 `[TestCase]`/`[TestCaseSource]` 전용 66개와 attribute-signature 사이 주석이
있는 `[Test]` 5개, 합계 최소 71개 method가 현재 discovery 밖에 있으므로 parser 확장 후 broad baseline을
다시 산출한다. 후속 PR 전에도 PR C에서 확인한 8개에는 method-level primary category를 직접
명시한다. 현재 checker가 보지 못한다는 이유로 제외하지 않는다.

## 10. 완료 조건

구성 3안은 다음 조건을 모두 만족해야 완료로 본다.

- PR A, B, C가 순서대로 main에 merge commit 방식으로 병합됨
- `3bf1adb62`, `d7f0164bb`, `0649e82ad`가 최종 main의 ancestor임
- 각 PR이 직전 main 대비 strict 신규 오류를 0개 추가함
- 각 PR 최종 HEAD에서 `core`와 지정 touched-cluster 검증이 완료됨
- asset/Prefab/Inspector 변경에 필요한 manual/editor evidence가 같은 revision에 귀속됨
- 각 PR final head의 required CI가 확인되고 actionable unresolved review가 0이며 mergeability/conflict를 재확인함
- evidence manifest의 HEAD/tree가 검증 worktree의 clean 상태와 일치함
- 과거 evidence와 새 PR evidence를 합쳐 하나의 validation claim으로 사용하지 않음
- TestCase parser 후속 PR은 세 기능 PR 완료의 선행조건이 아니며, 완료 후에도 project-wide inventory 또는
  governance closure를 주장하지 않음
- A ownership test와 B Controller fixture의 물리 placement debt가 별도 후속임을 non-claim으로 기록함
- broad full이 실행·통과하지 않은 상태에서 project-wide/full green을 주장하지 않음
