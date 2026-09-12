# Gameplay 최적화 브랜치 통합 계획

- 작성일: 2026-09-11 KST
- 상태: **검토 완료 / 통합안 제안 — 구현·병합·통합본 검증 미실행**
- 목적: 분리된 최적화의 출처와 의존성을 고정하고, 통합 시 보존할 동작·계측·증거 기준을 정한다.
- 범위: 아래 C/D 두 작업트리의 검토 당시 커밋 및 미커밋 변경. 저장소의 모든 브랜치를 조사한 목록은 아니다.
- 이번 작업: 문서화. 이 문서의 실행 절차는 향후 통합 계획이며 이미 실행된 작업을 뜻하지 않는다.
- 작업용 프롬프트: [통합 실행 프롬프트](./Gameplay-Optimization-Integration-Execution-Prompt-2026-09-11.md). 사용자 실행 요청 시 별도 통합 브랜치에서 수행하며, 작성·열람만으로 실행되지 않는다.

## 1. 결론과 통합 방식

전체 최적화를 함께 채택할 경우 **D의 고정 리비전에서 별도 통합 브랜치를 만들고 C HEAD를 merge하여 양쪽 이력을 보존**하는 방식을 권한다. D에는 Cleanup, 스냅샷, 정적 표시, AI, HUD와 계측의 선행 변경이 연결되어 있다. C에는 최근 드라이버 캐시와 Pause 검색 버퍼 재사용이 있다.

D는 기술적 출발점이며 모든 영역의 최종 정답을 의미하지 않는다. §3의 동작 변경을 통합 범위에 명시하고, C/D 미커밋 변경은 각각 보존·검토한 후 의도별로 결합한다. 파일 전체에 일괄 ours/theirs 선택을 적용하지 않는다. 일부 최적화만 선별 이식하는 경우에는 cherry-pick 또는 제한된 이식이 가능하지만, 그것은 본 문서의 전체 통합과 다른 범위다.

통합 기준 브랜치·최종 반영 대상 브랜치는 아직 확정하지 않았다. 현재 C 브랜치를 D 상태로 덮어쓰거나 기존 작업트리를 이동하는 계획이 아니다.

## 2. 검토 기준점과 적용 현황

C/D는 디스크에 따른 기능 구분이 아니라 아래 두 작업트리의 약칭이다. 경로는 검토 대상의 위치를 기록한 것이며 새 작업 공간 생성 예제가 아니다.

| 항목 | C | D |
|---|---|---|
| 작업트리 | `/mnt/c/users/user/2026teamproject_j2m-vfx-sfx` | `/mnt/d/J2M/worktrees/ui-callback-attribution` |
| 브랜치 | `codex/third-party-license-inventory` | `chore/ui-callback-attribution` |
| HEAD | `c150d0cc35efdadc50bf663bd59af4a52cee6b07` | `4565d93456d5a6832f438f390251508a961a8647` |
| 커밋 tree | `2c40e61d82ce12029d6f77063d0a6a7f3fb1f334` | `c10843711bf56a649bb669e006e3e01e1009ac12` |
| 공통 조상 이후 고유 커밋 | 2개 | 74개 |
| 공통 조상 대비 변경 파일 | 16개 | 339개 |

공통 조상은 `84004938c6e935a81adbafcb031d3fa63724933d`다. 위 tree는 **커밋된 소스만** 식별하며 미커밋 파일의 hash를 포함하지 않는다. 아래 미커밋 개수는 이 문서 작성 전 검토 시점이며 통합 실행 때 갱신한다.

| 구현 | C 기준점 | D 기준점 | 통합 권고 |
|---|---|---|---|
| 드라이버 조회 캐시·View 교체 상태 보존 | `261f2df07` 적용 | 해당 후속 변경 없음 | C 로직 + D 계측 |
| Pause 검색 List 재사용·재진입 배열 경로 | `c150d0cc3` 적용 | 미적용 | C 버퍼 수명 유지 |
| Pause reflection metadata 캐시 | 미적용 | `de208343e` 적용 | D 캐시를 두 검색 경로에 결합 |
| CrossLOS 반복 엔티티 List 복사 제거 | 미적용 | `fa4e95cb5` 적용 | D의 snapshot 읽기 경로 유지 |
| Tile cell index 불변 공유 / 동일 mutation epoch snapshot 재사용 | 해당 후속 변경 없음 | `34d4156da` / `e0597a490` 적용 | 관련 mutation·불변성 검증과 함께 유지 |
| Cleanup 후보 인덱스 / ordered 후보 실행 | 해당 후속 변경 없음 | `a2ee11779` / `7899692d5` 적용 | WorldState·snapshot·executor 의존 묶음 유지 |
| 정적 Wall target cache / 임시 컬렉션 재사용 | 해당 후속 변경 없음 | `cddd1eaed` / `5e0f0f23b` 적용 | Wall 표현 전환·무효화 계약과 함께 유지 |
| HUD 저장 읽기·Chance 갱신·query 재사용 | 해당 후속 변경 없음 | `a687a182a` / `76d13312f` / `0c555ba42` 적용 | H03 선행 정리와 연결된 묶음으로 검토 |

적용 여부는 해당 구현을 가리킨다. 예를 들어 D에도 기존 driver cache 코드가 있으므로 “D에는 어떤 캐시도 없다”는 의미가 아니다. 성능 검증 완료와 현재 브랜치 반영 완료도 구분한다.

## 3. 전체 통합에 포함되는 동작 계약

다음은 D 전체를 채택할 때 함께 들어오는 변화다. 성능 패치라는 이름으로 숨기지 않고 통합 채택표에 기록한다. 아래 표는 검토된 변경 내용이며 이번 문서화가 각 변경의 최종 채택을 의미하지 않는다.

| 변경 | 관련 커밋 | 확인할 계약 |
|---|---|---|
| authored Wall runtime type `None → Wall` | `8fb45beac` 및 선행 수용 변경 | Solid 점유·게임 규칙 보존, 허용된 type/name/hash 표현 변경 |
| HUD 조회가 입력 버퍼 deadline을 연장하던 부작용 제거 | `40e2576ce` | 입력 명령과 UI 읽기 경계, 기존 실제 입력 흐름 |
| 외부 저장 파일 변경의 관측 시점 변경 | `a687a182a` | 임의 HUD refresh 대신 실제 saved read·Reload·generic query/command 경계에서 관측 |
| death/clear 중간 Chance 저장값 표시 억제, pause 중 갱신 | `76d13312f` | 표시 scope 종료·pending 처리·LateUpdate 갱신 |
| 동일 Chance refresh의 pulse/cue 중복 방지 | `c607ce68f` | 값 유지와 실제 변경 시의 시청각 알림 |
| 미사용 PlayerStatus 표시 경로·HUD subtree 제거 | `9d9eea661` 등 H03 묶음 | 비표시 경로 삭제라는 기존 판단과 실제 HUD Prefab 상태 |

공통 조상부터 D까지 조사한 Scene/Prefab/asset/shader 및 Packages/ProjectSettings 범위에서 변경된 자산은 `GameplayHudRoot.prefab`이다. Wall 전환은 Stage 콘텐츠 파일의 재작성보다 `StageRuntimeBuilder`의 런타임 생성·수용 코드와 테스트 변경이다. Prefab 삭제 항목의 목적은 미사용 표시 경로 제거이며 실제 화면 검증을 별도로 남긴다.

## 4. 양쪽 미커밋 변경 보존

| 출처 | 검토 시점 현황 | 처리 |
|---|---|---|
| C tracked | 16개: production Factory fallback 제거, 테스트 수정, 옛 Slice3 제안 3개 파일 등 | 커밋과 별도로 보존하고 목적별 검토 |
| C untracked | `Windows.meta`, `PrimitivePresentationTestViewFactory.cs`와 `.meta` | 파일 bytes·GUID·존재 여부 보존 |
| D tracked | 6개 guard/process-handle 및 관련 Python·shell 테스트 | 구현·검증 상태를 확인해 별도 채택 |
| D untracked | `Windows.meta`, CPU-GPU 분석 계획, Late-Stage guard 수정 계획 | 문서·생성 metadata를 각각 분류 |

C 미커밋과 D 커밋 변경이 겹치는 9개 파일은 다음과 같다. 경로는 저장소 루트 기준이다.

| 경로 | 주요 검토 |
|---|---|
| `Assets/_Features/Gameplay/Gameplay_Host/Runtime/DefaultGameplayEntityViewFactory.cs` | fallback 제거 + Wall static admission |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTickPresentationCoordinatorTests.cs` | D 후속 회귀 수정과 C test factory 변경 |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTimingOwnershipTests.cs` | Wall·표시 공급 계약 |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs` | primitive 전제와 authored prefab 검사 분리 |
| `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/ActualSceneBootstrapSmokePlayModeTests.cs` | 실제 stage·소환·respawn 검사 보존 |
| `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PlayerMovementPlayModeTests.cs` | test factory의 Wall 지원 |
| `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-D1-E0-I3-Amendment.md` | D에서 삭제된 historical 제안 |
| `Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v5.md` | D에서 삭제된 draft contract |
| `Tools/contracts/gameplay_cleanup_slice3_evidence_review_vectors_v1.json` | 위 draft의 검토 vector |

통합 전에 양쪽 HEAD/tree, staged·unstaged binary patch, untracked 파일 bytes와 `.meta`, 경로별 SHA-256 및 파일 존재 여부를 **별도 D evidence 디렉터리**에 고정한다. 이후 입력이 달라지면 새 snapshot으로 재분류한다. 이 보존 절차는 아직 실행하지 않았다.

같은 이름의 C/D `Windows.meta`도 자동으로 동일 파일로 간주하지 않는다. 각 bytes·GUID와 최종 빌드의 생성 metadata 처리를 비교한다. 기존 작업트리에서 reset/clean하거나 전역 stash로 상태를 숨기는 방식을 기본 절차로 삼지 않는다.

옛 Slice3 3개 파일의 C 수정은 원문과 기준 HEAD/hash를 historical/non-active 자료로 보존한다. D가 완료·대체한 live contract에 그대로 복원하지 않는다. 보존은 통합본 재활성화와 다르며, 원본 파일 삭제를 이번 문서가 실행하거나 요구하지 않는다.

## 5. 확인된 결합 지점

### 5.1 커밋 간 텍스트 충돌

검토 중 `git merge-tree --write-tree --name-only`로 고정 C/D 커밋을 시험 결합했다. 결과 tree는 `24f8b46a659d2f094f4462697850839142656118`이며 충돌 파일은 아래 2개다. 이 tree에는 충돌 marker가 있으며 실행·빌드 기준이 아니다. 실제 브랜치/index/작업파일 병합은 하지 않았고 미커밋 변경도 포함하지 않았다.

| 파일 | 결합 기준 |
|---|---|
| `GameplayAnimationSyncCoordinator.cs` | D의 capture `SectionScope(DriverCache)`가 C의 `ResolveDrivers`와 missing-player cleanup을 감싸도록 결합. C에서 제거된 기존 3개 Cache helper 호출은 복원하지 않음 |
| `GameplayTickPresentationExtension.cs` | C의 List/배열 재진입 경로에 D reflection 캐시·카운터 결합. `Clear()`의 두 수명 계약 보존 |

Pause 결합의 구체 조건:

1. List와 배열 경로 모두 `TryCreate(behaviour, this, out target)` 형태로 같은 registry를 전달한다. C의 옛 2인자 호출을 남기면 D 정의와 불일치한다.
2. `ObservePauseRoot()`는 null 검사 후, 재진입 분기 전에 호출당 한 번 기록한다. 각 검색 직후 List는 `Count`, 배열은 `Length`로 후보 수를 기록한다. 배열 경로만 계측하면 일반 호출이 누락된다.
3. `Clear()`는 reflection metadata 캐시를 정리하되 활성 buffer와 `registrationBuffersInUse`는 변경하지 않는다. 바깥 소유 호출의 `finally`가 buffer 정리를 맡는다.
4. metadata 결과는 callback 전에 저장하고 Clear 이후에는 재조회한다. 캐시 수명이 바뀌는 호출까지 “타입당 한 번”으로 단정하지 않는다.
5. driver section 호출 수와 실제 component 조회 수는 다른 지표다. 캐시 hit로 조회가 줄어도 committed-frame section 방문 수를 같은 비율로 줄이지 않는다.

### 5.2 미커밋 변경의 의미 충돌

- Factory는 **D의 `EntityType.Wall` static prefab admission과 C의 missing-prefab 예외/fallback 제거**를 함께 보존한다.
- C의 새 `PrimitivePresentationTestViewFactory` 및 PlayMode test factory는 Wall type 허용과 visual profile 처리를 추가해야 한다. `None`만 Wall로 취급하는 fixture를 유지하지 않는다.
- D `DefaultGameplayEntityViewFactory_ProposedWall_PreservesWallMaterial` 등의 production primitive 전제는 C 변경과 양립하지 않는다. 실제 authored prefab의 renderer/material/mesh 보존과 missing-prefab 실패 검사를 분리한다. 테스트를 삭제하거나 synthetic factory로 전부 우회해 production admission 검증을 잃지 않는다.
- C ActualScene 추가분의 stage 0-2/1-2/2-2/3-2/3-3/4-2/4-3 첫 5 Tick, 소환 prefab, respawn replacement view 검사를 보존한다. D Wall/정적 캐시 조건도 함께 유지한다.

## 6. 통합본 검증 기준

각 검증은 최종 통합 revision 및 해당 입력 상태에 연결한다. 수정 전 D 또는 C의 PASS를 통합본 PASS로 승계하지 않는다. 실제 명령·필터·양수 실행 수·XML/log·미실행 이유를 기록한다.

| 검증 | 필요한 범위 |
|---|---|
| 기본 회귀 | 통합 작업트리에서 `./run_tests.sh core`, `./run_tests.sh ui` |
| Presentation/Pause | C driver cache EditMode·PlayMode, `GameplayPauseBufferReuseTests`, D `PauseCache_T01–T11` 및 Pause PlayMode |
| 새 결합 경계 | List 바깥 등록 + 배열 중첩 등록 + 동일 타입 cache hit의 root/후보/실제 reflection 조회 수; callback Clear 이후 재조회와 buffer 보존 |
| 미커밋 변경 관련 Full fixture | Coordinator, TimingOwnership, BoundsGuard, Audio/VFX migration, MoonBlock world-vs-view, LegacyDecommission, PlayerMovement, ActualSceneBootstrap, UI DestinationIris 변경의 해당 fixture |
| 실제 콘텐츠 | initial/소환/respawn/face 전환의 prefab 공급과 누락 시 실패; HUD Prefab·Chance 표시의 Editor/Player 확인 |
| Simulation | snapshot 불변성·mutation invalidation·Cleanup·CrossLOS·정적 Wall cache 및 관련 Replay 시나리오 |
| 일반/capture 빌드 | 두 설정의 컴파일·동작, capture 전용 writer와 validator 연결, parent/child 산술·카운터 의미 |
| D 도구 변경 채택 시 | Python guard/process-handle/diagnostics와 shell finalizer 회귀; Unity core/ui로 대체하지 않음 |
| 넓은 회귀 | 안정화/통합 전 `./run_tests.sh full`; baseline 실패와 통합으로 생긴 회귀를 분리 |

Full 분류 actual-scene/summon/respawn 검사는 core/ui만으로 실행됐다고 간주하지 않는다. `./run_tests.sh full --filter '<실제 대상 필터>'`로 필요한 fixture를 명시하고 실행 수가 0이면 검증 미완료로 기록한다. broad full이 red이면 관련 실패의 원인을 분류하고 미해결 touched regression을 남긴 채 통합 완료를 주장하지 않는다.

Replay/hash 비교는 두 종류다. C→D의 authored Wall 전환은 기존 migration 계약이 허용한 type/name/hash 차이를 검증한다. D 고정 소스→C의 presentation 캐시 결합은 simulation exact parity를 검증한다. 변경 후 canonical 재계산 일치와 반복 실행 결정성을 확인하며 과거 C raw hash와의 무조건 동일성을 요구하지 않는다.

## 7. 통합 성능 판정

통합본의 기능 검증 후 새 clean revision으로 late-stage smoke와 필요한 공식 paired capture를 진행한다. D의 기존 `gameplay-late-stage-performance` lane은 최종 통합 runner에 반영·검증된 경우에만 사용한다. C에 현재 없는 lane을 이미 실행 가능한 것으로 간주하지 않는다.

- Stage 4-2/4-3, 동일 장비·해상도·quality·frame cap·입력·warmup·Tick interval을 고정하고 실행 순서·표본·판정 기준을 수집 전에 기록한다.
- 비교 목적을 구분한다. D→통합본은 C 및 채택된 dirty 변경 전체의 차이를 평가하고, C→통합본은 D를 포함한 전체 채택 묶음의 차이를 평가한다. C 캐시만의 효과를 주장하려면 나머지 제품 소스·계측·도구를 동일하게 둔 별도 대조군을 만든다. 서로 다른 동작 계약이 포함되면 개별 캐시의 인과 효과로 해석하지 않는다.
- 새로운 campaign/ledger와 Player payload를 만든다. 이전 revision의 공식 campaign을 통합본으로 이어서 실행하지 않는다. 소스와 guard/tooling identity를 함께 고정한다.
- 부모 시간과 자식 시간을 중복 합산하지 않는다. 전체 프레임, 동기 Tick, Stage 간 증가분, 부모 내부 시간 비율을 구분한다.
- 성능 수집 성공과 성능 개선 판정은 별개다. allocation unavailable/무응답 counter를 0할당으로 해석하지 않는다. CPU와 GPU의 증거 적격성도 각 계약대로 구분한다.

과거 driver 조회 비용 감소, Pause 캐시 감소, AI 구간 감소를 더하거나 곱해 통합본의 FPS 개선률을 만들지 않는다. 기존 측정은 변경 채택 근거이며 통합본의 검증은 아니다.

## 8. 실행 순서와 완료 조건

1. 양쪽 HEAD·dirty 입력을 재확인하고 §4의 보존 자료를 만든다. 범위별 출처를 C commit/C dirty/D commit/D dirty로 기록한다.
2. §3의 동작 변경과 각 dirty 묶음을 채택·보류·대체 중 하나로 명시한다. 판단 이유와 최종 동작을 남긴다.
3. 새 작업트리는 `j2m-worktree-add`로 `/mnt/d/J2M/worktrees/` 아래에 만들고 D 고정 revision에서 통합 브랜치를 구성한다. 생성 전 D 여유 30 GiB 이상, C 여유 10 GiB 미만 경고, worktree별 private Library를 확인한다. 저장소 위치 준수 검토에는 `j2m-worktree-audit`를 사용한다.
4. C 고정 HEAD를 merge하고 §5의 계약을 결합한다. 검증 전 resolved project path가 `/mnt/d/J2M/worktrees/`로 시작하는지 확인한다.
5. 보존된 양쪽 dirty 중 채택 항목을 의도별로 반영한다. 각 결과에 출처·대체 이유·테스트 목적을 남긴다.
6. §6 검증을 실행하고 수정 후 최종 revision에 맞는 증거를 남긴다. 새 evidence는 `/mnt/d/J2M/evidence`, build는 `/mnt/d/J2M/builds` 아래 실행별 디렉터리에 저장한다.
7. §7의 새 성능 자료로 현재 비용 구성과 다음 후보를 갱신한다.
8. 통합 revision, 포함/제외 변경, 테스트 run/not-run, 성능 판정과 남은 backlog를 closeout에 기록한다. 최종 반영 대상은 그 구체 결과를 기준으로 정한다.

기능 통합 완료와 성능 개선 입증은 별도로 판정한다. broad full 결과 없이 project-wide green 또는 full-lane green을 주장하지 않는다. 사용자 변경의 보존과 모든 채택 항목의 추적이 확인되어야 통합 완료로 기록한다.

## 9. 문서 정리와 근거

통합 완료 후 하나의 revision을 기준으로 범주별 요약을 정리한다. 현재 C와 D의 상태를 한 문서에서 섞어 “현재 구현”으로 표현하지 않는다. Simulation·Presentation·UI 요약에는 revision, 측정 분모, 최종 판정, 미적용 항목, 원본 증거 링크를 둔다. 완료된 프롬프트·수정안은 현재 계측 계약을 분리한 뒤 historical 자료로 보관하는 것이 권고안이며 이번에 이동·삭제하지 않았다.

아래 D 링크는 **별도 작업트리의 참고 자료**다. 이동하거나 해당 브랜치가 변경되면 본 문서의 고정 commit에서 경로를 다시 조회한다. 통합 후에는 통합 저장소 내 상대 링크로 전환하고, 외부 evidence의 원본/hash는 유지한다. 원본 계획 상단의 PLANNED/HOLD보다 후속 실행·측정 증거가 나중 상태를 기록할 수 있다.

| 근거 | 용도 |
|---|---|
| [C driver cache 기록](./Gameplay-Presentation-Driver-Cache-Optimization-Plan.md) | 구현·View 교체·교차 측정 한계 |
| [C Pause List 이식](../Testing/Pause-Buffer-Reuse-Adoption-2026-09-11.md) | reflection 캐시 미포함, 이식 범위 |
| [D Post-Slice4 로드맵](/mnt/d/J2M/worktrees/ui-callback-attribution/Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Post-Slice4-Execution-Roadmap-2026-08-31.md) | Cleanup·snapshot·static cache의 진행·의존성 |
| [D Wall migration](/mnt/d/J2M/worktrees/ui-callback-attribution/Docs/Architecture/Gameplay-EntityType-Wall-Authoritative-Migration-Plan.md) | 허용된 표현 delta와 이후 parity |
| [D HUD 구현](/mnt/d/J2M/worktrees/ui-callback-attribution/Docs/Architecture/HUD-Query-Optimization-Implementation-2026-09-07.md) | 저장 관측·Chance·query 의존 묶음 |
| [D HUD 사용 조사](/mnt/d/J2M/worktrees/ui-callback-attribution/Docs/Architecture/HUD-Legacy-Usage-Audit-2026-09-07.md) | 입력 부작용·비표시 경로 제거 근거 |
| [D Pause 캐시 결과](/mnt/d/J2M/worktrees/ui-callback-attribution/Docs/Architecture/Gameplay-Pause-Registration-Optimization-2026-09-08.md) | reflection 캐시 효과와 전체 Tick 한계 |
| [D Committed Frame 계측](/mnt/d/J2M/worktrees/ui-callback-attribution/Docs/Testing/Committed-Frame-Cost-Attribution-2026-09-08.md) | root·후보·조회 카운터 의미 |
| [Level4 후속 6쌍 결과](/mnt/d/J2M/evidence/level4-six-slot-extension-20260906T063354Z/result.md) | AI 최적화 후 실제 측정, 단일 후보 선정 보류 |
| [현재 검증 운영](../Testing/Gameplay-Test-Automation-Guide.md) | lane·Full·증거 및 claim 규칙 |

### 이번 문서화의 검증 범위

수행 범위는 Git 이력·diff·시험 병합 tree·문서·기존 증거의 읽기 전용 검토와 본 문서/README 편집이다. 독립 검토 3개에서 통합 기반, 충돌/API/계측, 양쪽 dirty 보존·검증 기준을 대조했다. 실제 병합, runtime/asset/tool 수정, 양쪽 dirty 보존 artifact 생성, Unity core/ui/full 및 새 Player 측정은 수행하지 않았다. 문서 링크와 diff 형식만 이번 변경에서 확인하며 이는 기능·성능 검증을 대체하지 않는다.
