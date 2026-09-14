# Gameplay 최적화 통합 작업용 프롬프트

> **후속 검증 안내(2026-09-14):** 아래 본문은 작성 당시 계획·판정을 보존한다. 제품 통합과 후속 측정 도구의 `40a16e613` 반영, 실제 A/B 리비전, CPU 개선 신호와 측정 한계는 [Closeout §4](./Gameplay-Optimization-Main-Integration-Closeout-2026-09-14.md#4-검증과-측정-결과)에 기록했다. 새 bundle 결과로 historical official Cleanup Hold를 해제하지 않는다.

- 작성일: 2026-09-11 KST
- 상태: **Historical execution prompt — 실행 완료**
- 기준: [최적화 브랜치 통합 계획](./Gameplay-Optimization-Integration-Plan-2026-09-11.md)
- 후속 상태: 실행 결과는 [Gameplay 최적화 main 통합 Closeout](./Gameplay-Optimization-Main-Integration-Closeout-2026-09-14.md)에 기록됐다. 아래 내용은 당시 실행 경계와 입력 보존을 위한 기록이며 현재 상태에서 그대로 재실행하지 않는다.

## 작업 지시

최적화 통합 계획을 바탕으로 D의 고정 구현과 C의 후속 최적화, 양쪽 미커밋 변경을 별도 작업트리에서 통합하라. 계획만 다시 설명하고 끝내지 말고, 보존·구현·검증·결과 문서화까지 진행하라. 최종 산출물은 **양쪽 커밋 이력을 보존하고 채택한 변경을 검증한 로컬 통합 브랜치**다.

이 프롬프트를 실행하라는 요청의 범위는 새 작업트리·로컬 브랜치 생성, 로컬 merge/commit, 충돌 해결, 필요한 코드·테스트·문서 수정, 빌드·테스트·성능 수집이다. 기존 C/D 작업트리의 파일과 브랜치는 변경하지 않고, 기존 브랜치로의 최종 반영·원격 push·PR 생성은 포함하지 않는다. 사용자의 후속 지시가 범위를 바꾸면 그 지시를 따른다.

## 1. 먼저 읽고 입력을 확인하라

현재 세션의 AGENTS 지시와 각 작업트리의 적용 지시를 확인한다. 다음 문서를 읽고 이미 읽은 내용은 재사용한다.

- `Docs/Architecture/README.md`
- `Docs/Testing/Gameplay-Test-Automation-Guide.md`
- `AI_GIT_COMMIT_RULES.md`
- `Docs/Architecture/Gameplay-Optimization-Integration-Plan-2026-09-11.md`
- 통합 계획 §9에서 연결한 Wall migration, HUD 구현·사용 조사, Pause/Driver 기록

실행 시 gameplay 계약을 수정·검증하는 작업에는 사용 가능한 `gameplay-contract-hardening` skill을, 로컬 merge/commit 준비에는 `commit-push-workflow` skill을 읽고 적용한다. skill 사용이 원격 push 범위를 추가하지는 않는다.

검토 기준점은 다음과 같다. 경로는 기존 입력 작업트리이며 새 작업트리 생성 위치가 아니다.

| 입력 | 기존 작업트리 | 검토한 HEAD |
|---|---|---|
| C | `/mnt/c/users/user/2026teamproject_j2m-vfx-sfx` | `c150d0cc35efdadc50bf663bd59af4a52cee6b07` |
| D | `/mnt/d/J2M/worktrees/ui-callback-attribution` | `4565d93456d5a6832f438f390251508a961a8647` |

공통 조상은 `84004938c6e935a81adbafcb031d3fa63724933d`였다. 당시 고유 커밋 수 C 2/D 74, 커밋 간 충돌 파일 2개, C dirty와 D committed 중복 9개는 참고 수치다. 실제 수치를 맞추기 위해 변경을 버리지 않는다.

양쪽에서 `git status --short --branch`, `git diff --stat`, staged/unstaged diff, HEAD/tree와 공통 조상을 확인하라. HEAD가 바뀌었으면 기준 이후 diff와 의존성을 조사하고 선택한 입력 SHA를 명시하라. 관련 후속 수정·문서화는 근거를 남겨 포함할 수 있다. 서로 충돌하는 제품 결정이나 범위 밖 변경 때문에 입력 선택이 불가능하면 필요한 선택만 질문하고, 영향 없는 보존·조사는 계속한다. 변화가 있다는 이유만으로 같은 승인을 반복 요청하지 않는다.

계획·프롬프트가 아직 untracked이거나 D 고정 커밋에 없을 수 있다. 보존본을 먼저 확보하고 통합 브랜치에 함께 반영한다. 새 작업트리에서 두 문서를 찾지 못했다고 생략하지 않는다.

## 2. 양쪽 상태를 보존하고 작업 공간을 준비하라

새 evidence root는 `/mnt/d/J2M/evidence/optimization-integration-<실행 식별자>` 아래 exclusive 디렉터리로 만든다. C/D를 구분하여 다음을 보존하라.

- 실제 HEAD, tree, branch, status, staged/unstaged binary patch
- untracked 파일 bytes와 `.meta`, 각 경로의 존재 여부·SHA-256
- 적용 지시 및 선택한 계획·프롬프트 bytes/hash
- 입력 파일 목록과 source snapshot의 획득 시점

수집 전후 HEAD와 대상 파일 hash를 대조하여 일관된 입력인지 확인하라. 동시에 변경된 파일은 새 보존본으로 다시 고정한다. 입력 작업트리에 reset/clean/stash를 수행하거나 파일을 덮어쓰지 않는다. 실행 종료 시에도 입력 상태를 대조하고 외부 변경과 자신의 변경을 구분한다.

`j2m-worktree-audit`로 저장 위치 준수를 확인하고, 새 Unity 작업트리 생성 전 D 여유 공간 30 GiB 이상을 확인하라. C 여유 공간이 10 GiB 미만이면 알린다. 기존 C legacy 작업트리는 이동·삭제하지 않는다.

`j2m-worktree-add`의 실제 사용법을 확인한 뒤 `/mnt/d/J2M/worktrees/` 아래 새 작업트리를 만들고 선택한 D SHA에서 충돌 없는 이름의 `integration/gameplay-optimizations-<실행 식별자>` 로컬 브랜치를 시작하라. 직접 `git worktree add`를 호출하지 않는다. 작업트리마다 private Library를 사용하며 검증 직전에 resolved project path가 D worktree root 아래인지 확인한다. 새 build는 `/mnt/d/J2M/builds` 아래에 둔다.

## 3. 채택표를 만들고 통합 범위를 고정하라

전체 통합의 기본 범위는 계획 §2의 최적화 묶음과 §3의 D 동작 변경이다. D 전체를 성능 패치라고 뭉뚱그리지 말고 다음 의미를 채택표에 명시하라.

- authored Wall의 `None → Wall` runtime 표현과 허용된 hash/name 변화
- HUD 조회의 입력 버퍼 갱신 제거
- 외부 저장 변경을 관측하는 saved read·Reload·query/command 경계
- Chance 중간 표시 억제·pause 중 갱신·pulse/cue 중복 방지
- 비표시 HUD 경로·Prefab subtree 제거

위 동작을 되돌려야 한다는 새 근거가 있으면 문제와 대안을 제시하라. 차이가 있다는 이유로 자동 제외하거나 삭제된 옛 동작을 부활시키지 않는다.

각 변경에 `출처(C commit/C dirty/D commit/D dirty) / 기준 SHA 또는 파일 hash / 목적 / 채택·보류·대체 / 이유 / 필요한 검증`을 기록하라. 새로 발견한 범위 밖 변경은 원문을 보존하고 통합에서 분리한다.

기존에 검토한 dirty 변경은 다음 기준으로 처리한다.

| 변경 | 기본 처리 |
|---|---|
| C production primitive fallback 제거·관련 테스트 | Wall prefab admission과 결합해 반영 |
| C driver/Pause/actual-scene 등 관련 회귀 보완 | D의 후속 테스트와 의도별 결합 |
| D guard/process-handle 및 관련 테스트 | 실패 원인과 수정 의미를 확인한 후 도구 회귀와 함께 반영 |
| C 옛 Slice3 제안·contract·review vector 3개 | historical/non-active evidence로 보존; D live contract에 재활성화하지 않음 |
| 양쪽 `Windows.meta` | bytes/GUID와 생성 경로를 비교하여 판단; 이름만으로 합치지 않음 |
| 신규 계획·분석 문서 | 제안/구현/측정 완료 상태를 구분해 문서 묶음에 반영 |

## 4. 양쪽 이력을 merge하고 계약을 결합하라

통합 작업트리에서 선택한 C SHA를 merge하라. 충돌을 해결하고 diff를 검토한 뒤 로컬 merge commit으로 양쪽 ancestry를 보존한다. dirty 변경은 보존본에서 의도별로 적용하고 별도 로컬 커밋으로 추적한다. 필요한 보완은 로컬 후속 커밋으로 남긴다. 실제 commit 분리는 적용 skill과 저장소 규칙을 따르며, 과거 커밋을 재작성하지 않는다.

다음은 확인된 결합 조건이며 실제 diff에서 추가 문제도 조사하라.

1. **Driver:** D capture `SectionScope(DriverCache)`가 C `ResolveDrivers`와 missing-player cleanup을 감싸도록 한다. C에서 제거한 세 Cache helper 호출을 남기지 않는다. View 교체 상태 복원·파괴된 Driver·negative hit 계약을 유지한다.
2. **Pause API:** List와 재진입 배열 경로 모두 D의 `TryCreate(behaviour, this, out target)`을 통해 같은 reflection 캐시를 사용한다.
3. **Pause 계측:** root counter는 null 검사 이후·재진입 분기 이전 호출당 한 번, 후보 counter는 검색 직후 List `Count`/배열 `Length`로 기록한다. 일반 List 경로를 누락하지 않는다.
4. **Pause 수명:** callback 전에 metadata 결과를 저장한다. `Clear()`는 reflection 캐시를 비우되 활성 buffer/사용 표시는 건드리지 않는다. 소유 호출의 `finally`가 buffer를 정리하며 Clear 이후 재조회한다.
5. **Factory:** D의 Wall static prefab 수용과 C의 missing-prefab 예외를 함께 유지한다. synthetic primitive는 테스트 지원으로 남기고 production fallback으로 재도입하지 않는다.
6. **Wall 테스트:** C의 신규 EditMode/PlayMode factory가 `EntityType.Wall`을 처리하도록 한다. D의 primitive material 전제 테스트는 실제 prefab renderer/material/mesh 보존과 missing supply 실패 검사로 목적을 분리한다.
7. **실제 콘텐츠:** C의 초기 Stage 5 Tick·소환 prefab·respawn replacement view 검사와 D의 Wall/정적 cache 검사를 함께 보존한다.

파일 전체 ours/theirs 선택, 실패 테스트 삭제·skip, 계측 제거로 충돌을 숨기지 않는다. 자동 병합된 파일도 API·ownership·수명·호출 순서 관점에서 확인하라. driver section 방문 수와 실제 component 조회 수는 다른 지표로 유지한다.

## 5. 검증하고 통합으로 생긴 회귀를 해결하라

통합 작업트리의 `./run_tests.sh`를 사용한다. 원본 C/D에서 Unity를 실행하거나 고정 main projectPath로 증거를 만들지 않는다. 각 결과에 소스/도구 상태, 명령, 실제 필터, 실행 수, XML/log와 미실행 이유를 기록한다.

- `core`, `ui`를 실행한다.
- 계획 §6의 driver, Pause, Coordinator, TimingOwnership, BoundsGuard, Audio/VFX migration, MoonBlock, LegacyDecommission, PlayerMovement, ActualSceneBootstrap, DestinationIris 관련 fixture를 실제 이름으로 찾아 filtered Full을 실행한다. 필요한 actual-scene/summon/respawn 테스트의 실행 수가 0이면 검증하지 못한 것이다.
- List 바깥 등록 + 배열 중첩 등록 + 같은 타입 reflection hit, callback Clear + 재조회 + 외부 buffer 보존이 동시에 성립하는 통합 회귀를 확인한다. 기존 테스트로 증명되지 않는 결합 경계만 추가한다.
- 일반 빌드와 capture 빌드의 컴파일·실행 및 writer→validator 연결을 확인한다. scoped parent/child 산술·root/후보/실제 조회 counter의 의미를 검증한다.
- D 도구 변경을 반영하면 Python guard/process-handle/diagnostics 및 shell finalizer 회귀를 실행한다. Unity 통과로 도구 테스트를 대체하지 않는다.
- snapshot 불변성, invalidation, Cleanup, CrossLOS, 정적 Wall 및 Replay 시나리오를 실행한다. C→D의 허용된 Wall 표현 변경과 D→presentation 캐시 결합의 simulation exact parity를 구분한다.
- 안정화 단계에서 broad `full`을 실행한다. 기존 baseline과 touched regression을 구분하고, 통합이 만든 회귀는 수정한다. 원인을 확인하지 않고 모든 실패를 baseline으로 분류하지 않는다.
- Editor/Player에서 초기 생성·소환·respawn·face 전환의 prefab 공급과 HUD/Chance 표시를 확인한다. 환경상 확인하지 못한 범위는 미완료로 남긴다.

실패 후 같은 입력의 고비용 실행을 무작정 반복하지 않는다. 원인과 수정 범위를 먼저 좁히고 다시 검증하라. 소스 수정 이후 이전 revision의 PASS를 최종본 결과로 승계하지 않는다.

## 6. 통합 리비전의 성능을 측정하라

기능·계측 검증 후 소스를 로컬 커밋으로 고정하고, 최종 runner가 요구하는 clean/detached capture 조건을 충족하는 별도 D 작업트리에서 새 Player를 만든다. 실행 전에 사용할 lane, workload, 표본 수·순서·판정 기준을 기록한다. 기존 성능 계약을 그대로 사용할 수 없다면 먼저 그 차이를 구체화하고, 정식 기준이 없는 자료를 공식 PASS로 표시하지 않는다.

Stage 4-2/4-3의 고정 입력을 사용해 smoke부터 수행하고, 유효한 계측을 확보한 뒤 계획 §7에 맞는 paired capture를 수행하라. C의 기존 `gameplay-performance`와 D의 `gameplay-late-stage-performance`를 같은 계약으로 간주하지 않는다. 표본·GPU·allocation·프로세스 감시 조건을 완화해 PASS를 만들지 않는다.

새 campaign/ledger와 build payload를 사용하고 source/runtime/tooling identity를 함께 고정한다. 기존 revision의 campaign을 새 통합본으로 이어서 실행하지 않는다.

통합본 자체의 현재 비용 분해를 우선 산출하라. D 대조군과 비교하면 C 및 채택한 dirty 변경 묶음의 차이로 보고한다. C 캐시만의 효과를 주장하려면 나머지 제품 소스·계측·도구가 동일한 별도 대조군이 필요하다. 서로 다른 리비전의 비율을 곱하거나 과거 개선율을 합산하지 않는다.

결과 표에는 Simulation/Presentation/Callbacks, 필요한 하위 구간, ms/Tick과 부모 대비 비율을 기록하라. Stage 간 증가분 기여율, 실제 시간 점유율, 전후 변화율은 각 분모를 명시하여 분리한다. frame/GPU 시간과 동기 Tick을 섞지 않는다. 무응답 allocation counter는 0할당 증거가 아니다.

수집 실패·후보 선정 보류·개선 미확정은 그대로 보고한다. 성능 개선이 없다는 이유로 기능 통합 사실을 숨기거나, 기능 통합 성공을 성능 향상으로 바꾸어 말하지 않는다.

## 7. 검토와 결과 보고를 완료하라

도구와 세션에서 서브 에이전트 사용이 허용되면 서로 겹치지 않는 세 관점으로 최종 diff와 증거를 재검토시켜라: (1) simulation·동작 계약, (2) presentation·Pause·UI 수명, (3) 보존·도구·성능 증거. 서브 에이전트도 원본 작업트리를 수정하거나 별도로 Unity를 실행하지 않게 한다. 사용 불가하면 독립 검토 미실행 이유와 직접 확인 범위를 기록한다.

구체적인 지적은 구현·검증으로 해결하거나 근거를 들어 미해결로 남긴다. 통합본이 검증 가능해지기 전에 사용자에게 포괄적인 “진행할까요” 질문으로 작업을 넘기지 않는다. 실제 제품 결정 충돌, 필요한 입력 부재, 해결되지 않는 환경 제약이 있는 경우에만 필요한 질문과 완료된 산출물을 제시한다.

통합 브랜치에 계획·프롬프트와 closeout을 남기고 Architecture README에서 연결하라. closeout은 최소 다음을 포함한다.

- 통합 branch/HEAD/tree와 C/D 입력 SHA, 양쪽 dirty 보존 evidence
- 변경별 채택·보류·대체 결과와 ancestry 보존 확인
- 텍스트 충돌 및 의미 충돌의 최종 처리
- 실행한 검증·미실행 검증·이유·관련 baseline/새 실패
- 성능 원자료와 판정, 통합본 현재 비용 및 다음 최적화 후보
- 남은 기능·계측·문서 작업과 최종 반영 대상 미결정 사항

문서의 옛 PLANNED/HOLD를 현재 구현 상태로 복사하지 말고 최종 evidence를 연결한다. 폐기된 Slice3 제안을 활성 계약으로 되살리지 않는다. runtime 계약을 보존하기 전에 옛 계획 전체를 삭제·이동하지 않는다.

최종 답변은 통합 브랜치·commit, 포함한 변경, 검증 결과, 성능 결론, 남은 항목 순서로 간결하게 보고하라. 기존 C/D 브랜치 갱신·원격 push는 수행하지 않는다. 미실행 검증이 있으면 완료 범위를 한정하고, matching broad 검증 없이 project-wide green/full-lane green을 주장하지 않는다.
