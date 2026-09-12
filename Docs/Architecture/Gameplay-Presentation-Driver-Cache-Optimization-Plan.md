# Gameplay Presentation Driver Cache Optimization Plan

## 상태와 범위

- 작성일: 2026-09-10 KST.
- 상태: 세 driver 조회 캐시와 View 교체 처리를 구현했다. 아래 구현 기록에 최종 검증·성능 결과를 구분해서 기록한다.
- 검토 기준: HEAD `84004938c6e935a81adbafcb031d3fa63724933d`에 기존 미커밋 변경이 있는 작업트리. HEAD 단독의 검증 결과로 해석하지 않는다.
- 검토 방식: 서브에이전트 3명이 캐시/호출 경로, 생명주기, 캠페인 자산/테스트를 나누어 대조하고 통합 검토했다.
- 목적: 동일한 실제 View에 대한 세 presentation driver의 반복 검색을 줄이면서 기존 재생·정리 동작을 보존한다.

대상은 `GameplayAnimationSyncCoordinator`의 `EnemyAnimatorDriver`, `PlayerAnimatorDriver`, `EnemySummonScalePulsePresentationDriver` 조회다. Prefab 이름이나 유닛 타입으로 존재·부재를 하드코딩하지 않는다. 공통 coordinator이므로 캠페인 적 10종 외에 Player와 세 driver가 없는 View도 회귀 범위에 포함한다.

최초 문서화는 runtime/Prefab/Scene 변경을 포함하지 않았다. 후속 구현은 runtime과 테스트를 변경하며 Prefab/Scene 자산은 변경하지 않는다. authoritative simulation, AI, audio, semantic driver 배열 캐시의 구조 변경은 이 최적화의 대상이 아니다. 별도 Wall/Cleanup 최적화 문서의 실행 상태나 측정 계약을 변경하지 않는다.

## 구현 전 코드와 자산 검토 근거

아래 링크는 파일 단위 근거이며, 메서드명으로 위치를 식별한다. 기존 작업트리에는 factory의 primitive fallback 제거와 테스트용 factory 분리 변경 등이 있으므로 구현 전 최신 diff를 다시 확인한다.

| 근거 | 확인한 내용 |
|---|---|
| [GameplayCommittedFrameBuilder](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCommittedFrameBuilder.cs), `StoreCommittedEntityTargets` | 표시 가능한 엔티티의 View를 얻은 뒤 `CacheDrivers()` 호출. 적 전용 경로가 아님 |
| [GameplayTickPresentationCoordinator](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs), `StoreCommittedFrame` 호출부 | 초기화와 tick의 committed frame 구성 경로. render Update마다 세 검색을 수행한다고 표현하지 않음 |
| [GameplayAnimationSyncCoordinator](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs), `CacheDrivers`, 세 `TryGet…Driver`, `CachePlayerAnimatorDriver` | 명시적 캐시는 매번 세 검색을 수행하고, fallback은 부재를 저장하지 않음. Player 부재 처리에는 presentation 상태 제거도 포함 |
| 같은 coordinator의 `ApplyInitialPlayerPresentation`, `PreservePlayerFlipOutcomeState`, `AdvanceEnemyAutonomousPresentationAfterSemantic` | 기존 driver 사전은 실행 대상 순회와 직접 접근에도 사용됨 |
| [GameplayEntityPresentationApplier](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityPresentationApplier.cs), `ResolveEnemySemanticPresentationDrivers` | semantic driver 배열은 이미 entityId와 View instance 기준으로 캐시하며 빈 배열도 저장 |
| [GameplayEntityViewRegistry](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityViewRegistry.cs), `Register`, `Rebuild` | 같은 ID 등록은 기존 값을 교체. Rebuild는 등록·해제 이벤트 없이 목록을 재구성 |
| [SummonedEnemyPresentationResolver](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/SummonedEnemyPresentationResolver.cs), `CleanupOwnedViews` | 소유한 소환 View를 정리하면서 `ReleaseEntity()` 호출. 모든 일반 View 해제가 이 경로를 거친다고 가정하지 않음 |

### 캠페인 10종 루트 구성

[CampaignMain catalog](../../Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Catalogs/EnemyPresentationCatalog_CampaignMain.asset)의 등록 10종을 기준으로 prefab YAML의 script GUID와 `GameplayEntityView`가 붙은 동일 `m_GameObject`를 대조했다. 조회 범위는 coordinator의 루트 `TryGetComponent`와 같다.

| View | EnemyAnimatorDriver | PlayerAnimatorDriver | EnemySummonScalePulsePresentationDriver |
|---|---|---|---|
| Astreton | 있음 | 없음 | 없음 |
| BlackEye | 있음 | 없음 | 없음 |
| DrSaturn | 있음 | 없음 | 없음 |
| JPeter | 있음 | 없음 | 있음 |
| Kali | 있음 | 없음 | 없음 |
| Nebulous | 있음 | 없음 | 없음 |
| RocketFace | 있음 | 없음 | 없음 |
| SecBot | 있음 | 없음 | 없음 |
| Startis | 있음 | 없음 | 없음 |
| Sunwheel | 있음 | 없음 | 없음 |

같은 Prefabs 폴더에는 12개가 있다. Jumping과 PrototypeGravityFieldChaser는 이 catalog에 등록되지 않아 위 전수 대조 모집단에서는 제외한다. 공통 코드가 이들을 지원하지 않는다는 의미는 아니다.

[JPeter prefab](../../Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_JPeter.prefab)은 루트 ScalePulse 참조를 유지해야 한다. Kali·JPeter·SecBot의 직렬화된 Animator 참조가 비어 있어도 EnemyAnimatorDriver는 존재하며 내부 자식 Animator 탐색을 사용한다. Animator 필드가 비었다는 이유로 driver 호출을 생략하지 않는다.

| View | 별도 경로에서 보존할 구성·동작 |
|---|---|
| Astreton | 점프 presentation authoring, 이륙·착지 애니메이션 연계 |
| BlackEye | Pupil controller, Floating driver |
| RocketFace | Pupil controller, motion authoring |
| JPeter | ScalePulse와 semantic pause 연계 |
| SecBot | Semantic particle controller의 비활성 처리·복원 |
| DrSaturn | Utility cooldown aura VFX authoring |

이 구성과 semantic 배열 캐시는 세 driver 반복 검색과 구별한다. 캠페인 자산 표는 검토 시점의 관찰 결과이며 런타임 분기 조건으로 사용하지 않는다.

## 채택한 캐시 구조

coordinator 내부의 `Dictionary<int, DriverCacheEntry>`가 실제 View 참조와 nullable 세 driver 참조를 보관한다. 레코드 존재 자체가 세 조회의 완료를 뜻한다. `InitialPresenceMask`는 최초 존재 결과를 보관하며, 파괴된 positive는 실행 사전에서 제거하고 캐시의 해당 참조만 null로 바꾼다. 최초 부재와 파괴 후 부재를 구별하면서 같은 View에서 재검색하지 않는다.

기존 driver 사전은 존재하는 실행 대상만 유지한다. 기존 사전에 null을 삽입하여 부재를 표현하지 않는다. 조회 레코드와 실행 사전의 갱신·제거는 하나의 내부 경로에서 처리하여 불일치를 막는다. 조회·교체·제거는 coordinator 내부 resolver와 binding 제거 경로가 소유한다.

1. `CacheDrivers()`와 세 `TryGet…Driver()`가 공통 조회 규칙을 사용한다. 사전 등록 없이 TryGet부터 호출해도 같은 결과를 얻는다.
2. 실제 View가 같고 조회가 완료됐으면 존재·부재 결과를 모두 재사용한다. 정상적인 반복 조회에서 재생 상태를 초기화하지 않는다.
3. entityId가 같아도 실제 View가 달라지면 기존 참조를 반환하지 않는다. prefab identity나 entityId만으로 hit를 판단하지 않는다.
4. View 없음과 driver 부재를 구별한다. View를 나중에 공급했을 때 이전의 부재 결과 때문에 검색이 막히지 않아야 한다.
5. 기존 Player 부재 처리의 death override, visual hold, presentation state 정리는 검색 자체와 분리해 필요한 호출 경로에서 유지한다.
6. Unity의 파괴된 Object 참조를 최초 부재와 혼동하지 않는다. 호출 가능성 검사는 Unity Object의 생존 여부를 고려하며 `?.`만으로 대체하지 않는다.

### 구성 변경 정책

채택한 정책은 **공급 완료된 View의 최초 성공 resolve부터 세 driver 구성을 고정**하는 것이다. 구성은 등록 전에 완성한다. 같은 View에 나중에 추가한 component는 이번 수명에 반영하지 않으며, 파괴된 component는 호출 대상에서 제외한다. 새 구성을 사용하려면 View를 교체하거나 `ReleaseEntity()` / `Reset()`으로 기존 수명을 끝낸 뒤 다시 공급한다. 숨김·재등장은 새 수명이 아니다.

명시적 refresh API는 추가하지 않는다. 이 계약은 대상 세 driver에 한정되며 동적 semantic component 추가·제거를 지원한다는 의미가 아니다. 기존 semantic 배열 resolver는 그대로 유지한다. Registry 이벤트 없이 `Rebuild()`가 발생해도 현재 공급된 실제 View 참조를 비교해 교체를 감지한다.

## 생명주기와 통합 위험

| 상황 | 요구 동작 |
|---|---|
| 동일 View 반복 사용 | 참조·부재 캐시 재사용, 재생 진행 유지, 필요한 Player 부재 상태 정리 유지 |
| semantic pause | semantic 상태 적용 이후 autonomous Advance 순서 유지. pause 첫 프레임에 시간이 진행되지 않음 |
| GameObject 숨김·재등장 | 캐시 참조 재사용과 별개로 OnDisable 원복·재등장 animator 동기화 유지 |
| 같은 entityId의 View 교체 | old View 참조 분리, 살아 있는 old ScalePulse 기본 크기 복구, 새 View 참조 조회와 첫 presentation 적용 보장 |
| ReleaseEntity | 기존 ScalePulse 원복·entity presentation 상태 제거에 더해 새 조회 레코드도 제거 |
| Reset | 기존 전체 정리를 보존하며 모든 조회 레코드와 실행 사전 제거 |
| Release 후 동일 ID 또는 View 재사용 | 새 수명의 조회·초기 적용 수행. 이전 수명의 상태가 남지 않음 |

[ScalePulse driver](../../Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemySummonScalePulsePresentationDriver.cs)의 `Apply`는 windup/recover·취소·사망을 처리하고, `OnDisable`은 기본 크기와 내부 상태를 복원한다. [View binder](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityViewBinder.cs)의 숨김은 GameObject 비활성화다. 따라서 semantic pause의 진행 정지와 비활성화의 상태 초기화를 같은 동작으로 취급하지 않는다.

이번 구현은 다음 교체 문제를 함께 처리한다.

- 살아 있는 old ScalePulse는 binding 제거 시 기본 크기로 복구한다. View 교체는 `ReleaseEntity()`를 호출하지 않으므로 Player hold·death와 enemy utility track의 남은 시간을 보존한다. 새 driver에 마지막 지속 상태를 내부 `RestorePresentationState`로 복원하며 과거 이벤트를 다시 소비하지 않는다. ScalePulse 내부 경과 시간은 이전하지 않고, 새 View는 새 pulse 이벤트부터 재생한다. Player driver 부재 시에는 명시 캐시와 lazy 조회 모두 기존 상태 정리를 수행한다.
- `EntityPresentationApplySignature`에 실제 View 참조를 포함한다. 같은 entityId·pose·semantic이어도 새 View의 첫 semantic·pose·visibility 적용을 생략하지 않는다. 별도 identity 사전을 추가하지 않아 기존 signature 제거·Reset 경로가 View 참조도 함께 해제한다.
- lazy 조회가 실행 사전이나 Player 상태를 제거할 수 있으므로 hidden sync와 Player tick 적용은 재사용 가능한 목록을 통해 순회한다. 파괴된 Unity Object는 직접 실행 경로에서도 생존 여부를 검사한다.

## 구현 순서와 검증 기준

1. 현재 working-tree diff와 공급/교체/해제 경로를 다시 확인하고, 아래 동작별 테스트 기대값을 고정한다.
2. 공통 조회 레코드와 resolver를 도입하고 기존 실행 사전 및 Player 상태 정리를 유지한다.
3. View 교체·Release·Reset의 무효화와 old ScalePulse 복원, 새 View 첫 적용을 연결한다.
4. focused 회귀와 core를 실행하고 실제 prefab integration을 검증한다.
5. 조회 횟수와 성능을 측정하여 동작 보존 결과와 별도로 보고한다.

### 추가 검증 행렬

| 검증 | 합격 기준 |
|---|---|
| 캠페인 10종 실제 prefab | 루트 구성표 일치, factory/coordinator 초기·tick 적용, 각 별도 presentation 동작 보존 |
| 동일 View 반복 committed/tick | 최초 정상 resolve 이후 세 component 조회 횟수 증가 없음. 참조·부재 hit 모두 검증 |
| 등록 전 lazy lookup | TryGet부터 호출해도 공통 캐시 사용, 이후 CacheDrivers에서 재검색 없음 |
| View 없음 → 공급 | 이전 미공급 상태가 새 View 검색을 차단하지 않음 |
| 동일 ID 교체 | 있음→없음, 없음→있음, 있음→다른 참조 모두 반영. old driver 호출 중단 |
| 동일 ID·pose·semantic의 정지 적 교체 | 새 View에 semantic·pose·visibility가 처음 적용됨 |
| 실제 JPeter | windup/recover, 취소·사망 원복, pause/재개, 실제 SetActive 숨김·재등장, pulse 중 교체 후 old View 원복 |
| Player | action hold, death override, respawn, 숨김·재사용, driver 없는 View로 교체 시 정리 보존 |
| 세 driver 없는 View/Box | 부재 캐시 hit, 실행 목록에 null 없음, presentation no-op 유지 |
| Release·Reset | 같은 ID 새 View와 같은 View 재사용 모두 이전 참조·부재·재생 상태가 남지 않음 |
| 구성 정책 | 고정 계약의 위반 처리 또는 명시적 refresh 후 추가·파괴 반영. 파괴된 positive와 최초 부재 구별 |

### 기존 테스트 재사용 범위

- [EnemyViewPresentationMapperTests](../../Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyViewPresentationMapperTests.cs): ScalePulse windup/recover, semantic pause, disable 원복, JPeter YAML binding 검사. disable 검사는 OnDisable 직접 호출이므로 실제 SetActive lifecycle 검증을 추가한다.
- [GameplayTickPresentationCoordinatorTests](../../Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTickPresentationCoordinatorTests.cs): `GameplayTickPresentationCoordinator_SummonScalePulseFreezesDuringFrontFaceInactive`, `GameplayTickPresentationCoordinator_SummonCanceled_NormalizesScalePulse` 등. synthetic prefab 검증을 실제 JPeter prefab integration과 구별한다.
- [GameplayTimingOwnershipTests](../../Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTimingOwnershipTests.cs): 숨겨진 Player animator, respawn death override 등 Player 회귀에 활용한다.

구현 후 WSL에서 검증할 작업트리의 `./run_tests.sh core`와 위 touched fixtures 및 추가 fixture의 filtered lane을 실행한다. 예시는 `./run_tests.sh full --filter EnemyViewPresentationMapperTests`이며 나머지 fixture도 runner의 현재 필터 규칙으로 선택한다. filtered full 결과를 unfiltered full 결과로 보고하지 않는다. 실제 SetActive·prefab 통합은 필요한 PlayMode/editor 검증으로 보완한다.

새 증거는 `/mnt/d/J2M/evidence`, 새 build output은 `/mnt/d/J2M/builds`에 저장한다. 새 Unity worktree가 필요하면 `j2m-worktree-add`로 `/mnt/d/J2M/worktrees` 아래에 생성하고 저장소 정책 및 private Library 규칙을 따른다.

## 성능 판정과 남은 결정

현재 positive TryGet hit는 이미 검색을 생략한다. 예상 절감은 committed frame 구성 시 세 재검색과 missing driver fallback 조회다. 조회 횟수 감소를 프레임 시간 개선과 동일시하지 않는다.

- 동일 workload에서 최초 resolve, 반복 hit, refresh 횟수와 실제 component 조회 횟수를 나누어 기록한다. 테스트용 계측은 정상 실행에 불필요한 per-frame 할당을 추가하지 않는다.
- 변경 전후 같은 환경·콘텐츠·entity 수·tick 수·warm-up 조건으로 coordinator/committed frame CPU 시간과 GC allocation을 비교한다. 각 결과의 revision 및 working-tree 상태를 기록한다.
- 반복 검색 제거는 구조적 합격 기준이다. CPU 개선이 측정 오차 범위에 있으면 검색 감소만 보고한다. 캐시 유지 비용과 초기화 비용도 함께 평가한다.
- 수치 성능 합격 기준과 반복 측정 횟수는 아래 측정 계약으로 확정했다. 변경 전후 관측값은 최종 결과 표에 기록하며 조회 감소와 CPU 개선 판정을 분리한다.

구성 고정, 지속 상태 복원과 pulse 진행률 비이전, signature의 View identity 포함 정책을 채택했다. 성능 측정 방법과 실제 실행 결과는 아래 구현 기록을 따른다.

## 최초 문서화의 검증 기록 (구현 전 이력)

- 실행: 작업트리 상태·diff 확인, 이전 코드/자산 검토 결과 대조, 문서 상대 링크 존재 확인, 문서 변경 whitespace 검사.
- 미실행: `./run_tests.sh core`, `ui`, focused/full lane, Unity editor/PlayMode, Player build 및 성능 측정. 이번 변경은 문서 추가와 README 연결만 포함하므로 실행하지 않았다.
- 허용 결과: 검토 내용과 구현·검증 계획의 문서화. 기능 회귀 통과, 성능 개선, broad lane 복구는 미확인이다.
- full lane의 문서화된 baseline은 red이며 이후 touched 결과와 분리한다. 검증 운영 기준은 [Gameplay Test Automation Guide](../Testing/Gameplay-Test-Automation-Guide.md)를 따른다.

## 구현 및 검증 기록 — 2026-09-10

### 변경 범위와 인터페이스

- runtime: 공통 세 driver resolver, positive/negative 캐시, 파괴된 참조 제외, binding 교체와 entity release 분리, Player 부재 정리, 새 View 첫 적용 보장.
- 기존 public 시그니처와 serialized 필드는 유지한다. 테스트가 공통 조회를 직접 검증하도록 세 TryGet을 internal로 열고 opt-in 정수 진단 카운터를 추가했다. Enemy/Player driver의 지속 상태 복원도 internal이다.
- 기존 factory·테스트 변경과 Wall/Cleanup 문서 변경은 이번 작업 전의 미커밋 변경으로 보존한다. Scene/Prefab/ScriptableObject 자산 변경은 없다.
- 신규 fixture: `GameplayAnimationDriverCacheTests`, `GameplayDriverCachePlayModeTests`, opt-in `GameplayDriverCachePerformanceTests`. 기존 touched fixture 세 개를 함께 검증한다.

### 측정 계약

- 실제 캠페인 적 10종, 실제 Player prefab, 세 driver가 없는 테스트 Box의 12개 cohort 및 10배인 120개 cohort. prefab 생성과 등록은 반복 측정 밖에서 끝낸다.
- 변경 전후 각각 독립 runner 실행 5회. 각 실행은 200회 warm-up 후 2,000회 `CacheDrivers` 순회와 `StoreCommittedFrame`을 따로 측정한다. 최초 resolve·Reset·Reset 후 resolve는 각 1회로 별도 기록한다.
- CPU 시간은 Stopwatch로 측정한다. 중앙값이 5% 이상 감소하고 실행 간 범위가 겹치지 않을 때만 CPU 개선을 확정한다. 최초 호출의 JIT·초기화 비용은 반복 hit와 합치지 않는다.
- 이 Unity Editor에서 `GC.GetAllocatedBytesForCurrentThread` 결과는 실제 할당 경로에서도 0이므로 byte-volume 근거로 사용하지 않는다. CPU 로그의 `managed_counter_raw_delta`는 참고용이며 초기 before 로그의 `allocated_bytes=0`도 같은 무효한 계측값이다.
- GC는 [Unity의 current-thread GC.Alloc recorder 예제](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread.html)에 따라 **할당 이벤트 수**를 별도 수집한다. marker sample Value는 시간이므로 byte 수로 해석하지 않는다. 각 표본에 4 KiB 배열의 positive control과 empty control을 포함하고, recorder 유효성과 버퍼 미포화를 검사한다. 반복 캐시 경로는 이벤트 0을 요구한다. 5개 GC 표본은 cohort별 같은 Editor 실행 안에서 수집하며 독립 CPU 실행 5회와 구별한다.
- 대상 component 조회 횟수는 opt-in 카운터로 검증한다. 정상 성능 측정에서 카운터는 꺼져 있다. 초기 성공 resolve는 3회이며 같은 View의 재검색은 0회다. mapper나 다른 presentation component의 조회는 이 카운터 범위가 아니다.
- 새 증거 루트: `/mnt/d/J2M/evidence/20260910-driver-cache/`. HEAD·사전 diff·대상 source hash와 XML/로그를 보관한다. 사용 중인 legacy C 작업트리를 그대로 검증한다.

### 실행 상태

컴파일 오류, 선택된 테스트가 없는 실행, GC 단위 확인 과정의 실패는 최종 통과 증거로 사용하지 않는다.

- `baseline-1`: WSL→Windows 환경 변수 전달 누락으로 performance 2개 skipped. 측정 증거에서 제외했다. 유효한 측정은 `WSLENV`에 opt-in 변수를 명시한 `before-1`부터 시작한다.
- `before-gc`: `GC.Alloc` marker Value를 byte 수로 해석한 잘못된 양성 대조군이 실패했다. 해당 결과는 폐기하고 allocation event count와 empty/positive control을 사용한 `before-gc-count`로 대체했다.
- `focused-1`: 신규 테스트의 TickTrace namespace 누락으로 Unity compile 실패. `focused-2`: 잘못된 pipe 필터로 선택 0개, runner 실패. 둘 다 유효한 회귀 실행이 아니다. 현재 fixture 다중 선택 구분자는 `;`다.
- `focused-3`: 신규 EditMode 26개 중 25개 통과, 1개는 synthetic Player timing authoring 누락. 테스트 공급 구성을 고쳤다.
- `focused-final`: 신규 EditMode 28개 통과, PlayMode 1/2 통과. pause probe에 명시적인 pause 플래그가 빠진 테스트 입력을 수정했고 runtime은 변경하지 않았다. 최종 증거는 `focused-final-2`다.
- `touched-1`: EditMode 388개 중 380개 통과, 기존 fixture 실패 8개. 당시 신규 26개는 모두 통과했다. EditMode 실패로 그 실행의 PlayMode는 실행되지 않았다.
- `existing-failures-before`: 기존 사용자 diff를 유지하고 이번 runtime 네 파일만 원래 코드로 되돌린 대조 실행에서 같은 8개를 모두 재현했다. 신규 캐시 fixture만 이 대조 실행의 컴파일에서 제외했으며 완료 후 구현·테스트를 byte-for-byte 복원했다. 실패 test fullname과 메시지가 정확히 같다는 비교는 `baseline-failure-comparison.json`에 기록했다.

기존 실패 8개는 inactive/Gravity emission 관련 폐기된 serialized field·shader 계약 7개와 `PlayerActionAnimation_HostPath_PreservesSemanticRequests`의 다른 enum 타입 간 비교 1개다. 이번 캐시 최적화의 수정 범위에 포함하지 않는다.

최종 구현의 신규 회귀·core·성능·관련 fixture 재실행 결과는 다음과 같다. 모든 runtime/test source와 workload hash는 최종 확인에서도 일치했다.

### 최종 검증 결과

| 실행 | 결과 | 증거 디렉터리 |
|---|---|---|
| 신규 캐시/생명주기 filtered full | editmode: 28 total / 28 passed / 0 failed / 0 skipped; playmode: 2 total / 2 passed / 0 failed / 0 skipped | `focused-final-2/test-results/` |
| core | editmode: 228 total / 228 passed / 0 failed / 0 skipped; playmode: 111 total / 107 passed / 0 failed / 4 skipped | `core/test-results/` |
| 관련 fixture 전체 filtered full | EditMode 390 total / 382 passed / 8 기존 실패; 이 실행의 PlayMode는 EditMode 실패로 미실행 | `touched-final/test-results/` |
| CPU 고정 cohort 비교 | before/after 각각 독립 실행 5회, 매회 두 cohort 통과 | `before-1`…`before-5`, `after-1`…`after-5` |
| GC 이벤트 비교 | 각 cohort 5표본, 모든 positive/empty control 및 cache-hit 0 검사 통과 | `before-gc-count`, `after-gc` |

실행 명령은 같은 작업트리의 `./run_tests.sh`를 사용했다. 공통으로 `CODEX_VALIDATION_ROOT`를 위 증거 루트의 해당 하위 디렉터리로 설정했다.

```bash
./run_tests.sh core
./run_tests.sh full --filter 'GameplayAnimationDriverCacheTests;GameplayDriverCachePlayModeTests'
./run_tests.sh full --filter 'GameplayAnimationDriverCacheTests;GameplayDriverCachePlayModeTests;EnemyViewPresentationMapperTests;GameplayTickPresentationCoordinatorTests;GameplayTimingOwnershipTests'
WSLENV="${WSLENV:+$WSLENV:}J2M_DRIVER_CACHE_PERFORMANCE" J2M_DRIVER_CACHE_PERFORMANCE=1 ./run_tests.sh full --filter GameplayDriverCachePerformanceTests
WSLENV="${WSLENV:+$WSLENV:}J2M_DRIVER_CACHE_PERFORMANCE:J2M_DRIVER_CACHE_ALLOCATION" J2M_DRIVER_CACHE_PERFORMANCE=1 J2M_DRIVER_CACHE_ALLOCATION=1 ./run_tests.sh full --filter GameplayDriverCachePerformanceTests
```

### 성능 결과

아래 시간은 2,000회 workload 전체의 ms이며 프레임 시간이 아니다. 괄호는 독립 실행 5회의 min–max다. 전체 원자료와 초기화/Reset 별도 수치는 `summary.json`, `before-cpu.json`, `after-cpu.json`에 있다.

| View 수 | 측정 구간 | 변경 전 중앙값 (범위), ms | 변경 후 중앙값 (범위), ms | 중앙값 감소 | 판정 |
|---|---|---|---|---|---|
| 12 | `cache-hit` | 14.524 (14.206–19.355) | 2.822 (2.719–3.002) | 80.6% | 측정 workload 내 개선 |
| 12 | `committed-frame` | 39.848 (39.271–80.430) | 25.033 (24.686–120.481) | 37.2% | 변동 범위로 미확정 |
| 120 | `cache-hit` | 150.263 (149.252–189.903) | 28.945 (27.838–30.843) | 80.7% | 측정 workload 내 개선 |
| 120 | `committed-frame` | 482.849 (397.033–1322.334) | 250.202 (240.353–258.539) | 48.2% | 측정 workload 내 개선 |

- 반복 캐시 조회는 처음 resolve한 뒤 대상 component 추가 조회 0회이며, GC allocation event도 양쪽 cohort의 모든 표본에서 0회다.
- committed-frame은 변경 전후 모두 2,000회당 GC allocation event 4,000회다. 기존 frame 구성의 할당은 그대로이며 이 최적화가 추가하지 않았다. byte-volume 감소를 주장하지 않는다.
- 최초 resolve는 캐시 사전 초기화와 JIT를 포함하므로 별도 진단값으로만 보관한다. 새 조회 사전은 View 수명당 레코드를 추가하므로 초기화·보관 비용이 있다. Release/Reset은 참조와 레코드를 해제한다.

### 허용 결과, 미실행 범위, 남은 항목

- 허용: `core lane validated`, 신규 캐시/실제 JPeter lifecycle 검증 통과, 명시된 Editor workload의 조회 감소·CPU 측정 결과·GC 이벤트 보존.
- 미실행: unfiltered full, UI lane, standalone Player build/성능 측정, 수동 렌더링/카메라/화질 확인. UI·자산 변경이 없으며 이번 검증은 해당 작업트리의 자동화와 Editor 성능 범위다. 실제 SetActive/OnDisable은 PlayMode로 검증했다.
- 명시적 비주장: broad/full lane 복구, UI 회귀 통과, 실제 게임 전체 frame-time/FPS 개선, GC byte-volume 측정, 동적 semantic component 변경 지원.
- 열린 기능 backlog: 재현된 기존 8개 실패는 emission/shader 계약과 enum 비교 테스트의 별도 후속 범위로 남긴다. 새 캐시 fixture의 열린 실패는 없다.
- 열린 제한: 공급 후 세 driver 구성 고정 계약을 따른다. 같은 View에 component를 추가하려면 기존 수명을 끝내야 한다. 이전 ScalePulse 진행률은 새 View에 이전하지 않는다. Editor 측정값을 Player 수치로 일반화하지 않는다.
- 근거: `summary.json`, `final-source-hashes.json`, `workload-hashes.json`, `baseline-failure-comparison.json`, `user-change-preservation.json` 및 각 실행의 XML/로그. `final-source/`에는 최종 runtime/test 소스도 보관했다. 모든 증거 날짜는 2026-09-10이며 각 XML에 정확한 UTC 시작·종료 시각이 있다.

## 서브 에이전트 재검토 후 상태 복원 보완 (2026-09-10)

### 수정 범위와 동작

재검토에서 같은 entityId의 새 View에 Enemy 상태 값·속도만 복원되고 실제 Death/utility Windup 상태 진입이 누락됨을 확인했다. DrSaturn의 실제 Controller는 이 전이를 trigger로 구동하므로 일반 파라미터 동기화로는 복원되지 않았다.

- 새 View의 Death와 유지 중인 utility phase는 `Animator.Play`로 직접 초기화한다. 이전 일회성 신호나 trigger를 재발행하지 않고 기존 public API는 유지한다.
- utility phase는 coordinator track의 경과 시간 비율에서 시작한다. 반복 캐시 조회는 재생을 재시작하지 않으며 기존 track의 종료 시점은 유지한다. 이전 ScalePulse 진행률은 여전히 이전하지 않는다.
- 비활성/paused Animator는 복원 요청을 보관한다. utility가 대기 중 끝나면 요청을 취소하고, 대기 중 사망하면 Death로 승격해 utility 만료 이후에도 사망을 복원한다.
- 사망 carrier에 이전 jump/glide phase가 남아 있어도 runtime sync/resync가 Death를 덮어쓰지 않게 한다.
- 변경은 EnemyAnimatorDriver, GameplayAnimationSyncCoordinator, 기존 PlayMode 캐시 fixture 및 이 기록에 한정한다. Prefab/Scene/Controller 자산은 변경하지 않았다.

### 검증과 증거

증거 루트는 `/mnt/d/J2M/evidence/20260910-driver-cache-rebind-fix/`다. 기존 성능 결과는 보완 전 소스의 이력이며 이번 상태 복원 보완의 성능 재측정 결과로 사용하지 않는다.

```bash
./run_tests.sh full --filter GameplayDriverCachePlayModeTests
./run_tests.sh full --filter 'GameplayAnimationDriverCacheTests;GameplayDriverCachePlayModeTests'
./run_tests.sh core
./run_tests.sh full --filter 'GameplayAnimationDriverCacheTests;GameplayDriverCachePlayModeTests;EnemyViewPresentationMapperTests;GameplayTickPresentationCoordinatorTests;GameplayTimingOwnershipTests'
```

각 실행은 동일 작업트리에서 `CODEX_VALIDATION_ROOT`를 증거 루트의 실행별 하위 경로로 지정했다. `red`는 테스트 입력 생성 코드의 컴파일 실패로 기능 증거에서 제외했다. `red-2`는 수정 전 runtime에서 실제 Animator 상태 assertion 4개 실패, 기존 2개와 만료 대기 요청 검사 1개 통과를 확인했다. `focused`는 첫 수정본 EditMode 28개와 PlayMode 7개가 통과한 중간 이력이다. 이후 대기 중 사망 및 사망 시 airborne 잔존 검사를 추가했다.

최종 소스에서 실행한 결과:

| 실행 | 결과 | 증거 |
|---|---|---|
| 캐시 focused | EditMode 28/28 통과, PlayMode 9/9 통과 | `focused-final/test-results/` |
| core | EditMode 228/228 통과, PlayMode 111 total / 107 passed / 0 failed / 4 skipped | `core/test-results/` |
| 관련 fixture | EditMode 390 total / 382 passed / 8 기존 실패; 이 실행의 PlayMode는 EditMode 실패로 미실행 | `touched/test-results/` |

추가한 DrSaturn PlayMode 7개는 활성·비활성 Death 복원, 활성·비활성 utility phase/진행률 복원, 비활성 대기 중 utility 만료, 대기 중 사망 후 utility 만료, 사망 시 airborne 정보 잔존을 다룬다. 새 View의 실제 Animator 상태를 검사하며 반복 조회 후 진행률 유지·신호 카운터 비증가·컴포넌트 추가 조회 없음도 확인한다. 서브 에이전트의 후속 정적 검토에서 발견한 대기 utility보다 사망이 늦게 도착하는 경로도 보완하고 재검토했다.

기존 실패 8개는 `/mnt/d/J2M/evidence/20260910-driver-cache/existing-failures-before/`의 변경 전 runtime 실행과 fullname·오류 메시지가 정확히 일치한다. `summary.json`에 대조 결과와 각 XML의 UTC 실행 시각을 기록했다. `final-source-hashes.json`, `final-source/`, `fix.patch`, `preexisting-diff-preservation.json`에 검증 소스와 변경 경계 증거를 보관했다. 이번에 수정하지 않은 기존 tracked diff 19개는 그대로 보존했고 `git diff --check`도 통과했다.

허용 결과는 이번 상태 복원의 targeted 검증 및 `core lane validated`다. unfiltered full, UI, standalone Player, 수동 렌더링 확인과 CPU/GC 재측정은 실행하지 않았다. 이번 작업은 presentation 코드·테스트 보완이며 자산 변경은 없다. 전체 회귀 복구나 새 성능 개선 수치를 주장하지 않는다. 열린 backlog는 기존 emission/shader 계약 및 enum 비교 실패 8건이며, 이번 추가 회귀 테스트의 열린 실패는 없다.


## 최종 수정본 CPU·GC 재측정 (2026-09-10)

최종 상태 복원 보완까지 포함한 성능 근거는 이 절과 `/mnt/d/J2M/evidence/20260910-driver-cache-final-performance/`를 따른다. runtime·테스트 코드는 수정하지 않고 기존 benchmark를 그대로 실행했다. 이전 workload의 prefab·catalog·factory 15개 hash가 모두 일치했고 측정 전후 대상 소스 7개와 workload hash 및 tracked diff가 유지됐다.

### 실행과 비교 조건

- 동일 legacy 작업트리의 `./run_tests.sh full --filter GameplayDriverCachePerformanceTests`를 사용했다.
- CPU: `J2M_DRIVER_CACHE_PERFORMANCE=1`, `J2M_DRIVER_CACHE_ALLOCATION=0`을 WSLENV로 전달하여 독립 runner 5회 실행. 매회 EditMode 2/2 통과, 선택된 PlayMode 0개. 총 CPU fixture 10회 통과.
- GC: `J2M_DRIVER_CACHE_ALLOCATION=1`로 별도 runner 1회 실행. EditMode 2/2 통과, 선택된 PlayMode 0개. cohort별 같은 Editor 실행 안에서 5표본과 positive/empty controls를 검사했다.
- warm-up 200회, workload 반복 2,000회, View 12개·120개 및 초기화/Reset 별도 계측 조건은 이전 측정과 같다. GC 실행의 CPU 표본은 CPU 5회 집계에 섞지 않았다.
- 비교군은 같은 날 기록한 `20260910-driver-cache/before-1`…`before-5` 및 `after-1`…`after-5`다. 비교군 소스를 이번에 다시 실행하지 않았으므로 같은 workload의 과거 실행과 비교한 결과다.
- 기존 판정 기준(중앙값 변화 5% 이상 및 표본 min–max 비중첩)을 유지했다. 큰 편차가 있는 표본도 제외하지 않았다.

### CPU 결과

시간은 **2,000회 workload 전체의 ms**다. 괄호는 독립 실행 5회의 min–max다.

| View 수 | 구간 | 최적화 전 중앙값 (범위), ms | 최종 중앙값 (범위), ms | 중앙값 감소 | 판정 |
|---|---|---|---|---|---|
| 12 | `cache-hit` | 14.524 (14.206–19.355) | 2.901 (2.769–3.294) | 80.0% | 측정 workload 내 개선 |
| 12 | `committed-frame` | 39.848 (39.271–80.430) | 25.783 (23.514–145.286) | 35.3% | 편차로 미확정 |
| 120 | `cache-hit` | 150.263 (149.252–189.903) | 28.472 (27.566–30.054) | 81.1% | 측정 workload 내 개선 |
| 120 | `committed-frame` | 482.849 (397.033–1322.334) | 255.910 (234.432–391.641) | 47.0% | 측정 workload 내 개선 |

상태 복원 보완 전 최적화본과 비교한 중앙값 변화는 12개 cache-hit +2.8%, committed-frame +3.0%, 120개 cache-hit −1.6%, committed-frame +2.3%다. 네 구간 모두 표본 범위가 겹치며 기준상 개선·회귀가 확정되지 않는다. View 교체 시 Animator 상태 복원 비용이나 활성 utility track 갱신 비용을 따로 측정한 결과는 아니다.

### GC 결과와 검증 범위

두 cohort 모두 5표본에서 positive control은 1회, empty control은 0회, cache-hit은 0회, committed-frame은 4,000회의 allocation event를 기록했다. workload당 2,000회 반복 조건이며 기존 측정과 같다. recorder의 유효성과 버퍼 미포화를 검사했다. GC byte-volume은 측정하지 않았다.

원자료는 `cpu-1`…`cpu-5`, `gc`의 XML/로그다. `summary.json`에 정확한 UTC 실행 시각, 중앙값·범위·전체 표본·비교 판정을 기록했고 `final-cpu.json`, `final-gc.json`, `source-hashes.json`, `workload-hashes.json`, `historical_input_hashes`로 입력을 추적할 수 있다. 최초 resolve·Reset 수치는 `summary.json`의 진단값으로 보관한다.

허용 결과는 이 Editor workload의 CPU·GC 측정 및 이전 이력과의 비교다. 실제 게임 전체 frame-time/FPS, standalone Player 성능, GC 바이트 감소는 검증 범위에 포함하지 않는다. 이번 작업에서는 소스 변경이 없어 core·행동 회귀를 재실행하지 않았으며, 동일 최종 소스의 앞 절 검증 기록을 유지한다. unfiltered full·UI·수동 렌더링 검증은 미실행이고 기존 실패 8건의 backlog는 그대로다.


## 전체 tick·실제 Player 프레임 측정 (2026-09-10)

### 범위와 실행 조건

최종 작업트리의 Windows Mono Player를 기존 `./run_tests.sh gameplay-performance` lane으로 빌드·실행했다. `stage-1-1` canonical gameplay shell, PC 품질, Direct3D11, 1920×1080 창 모드이며 VSync·target frame cap을 해제했다. 하드웨어는 i5-13500 / RTX 4060 Ti 8GB / 약 32GB RAM이고 Unity는 6000.3.11f1이다. `BuildOptions.None`에 capture capability와 Frame Timing Stats를 활성화한 ReleaseLikeCapture다.

120프레임 warm-up 및 tick admission 준비 후 `render-idle`과 `gameplay-neutral-tick`을 각각 600프레임 수집했다. gameplay 구간은 이동 입력 0, 6프레임마다 tick 실행을 시도해 100/100 tick이 실행됐다. CPU main/render와 GPU는 각 구간에서 600/600 유효 표본을 확보했다. 최종본의 단일 성공 캡처이며 최적화 전 전체 tick·프레임과의 A/B 비교는 수행하지 않았다.

`tickWallMilliseconds`는 `GameplayInputHost.RunSingleTick()` 진입부터 반환까지 Stopwatch로 측정한다. 입력 command 기록, `TickRunner.RunNextTick()`, 결과 후처리, 동기 `Presenter.Present(result)`, TickCompleted/ObjectiveResultUpdated 이벤트를 포함한다. 이후 프레임별 presentation advance·Animator·렌더링은 실제 프레임 측정에 포함된다. 시뮬레이션과 Presenter 각각의 배타적 시간은 이 계측에서 분리하지 않았다.

### Release형 Player 결과

단위는 ms다. CPU/GPU 스레드 시간은 겹칠 수 있으므로 합산하지 않는다.

| 구간 | 지표 | 표본 | 중앙값 | p95 | p99 | 최대 |
|---|---|---|---|---|---|---|
| `render-idle` | 실제 프레임 간격 | 600 | 2.922 | 3.934 | 4.246 | 5.781 |
| `render-idle` | CPU main | 600 | 2.280 | 3.087 | 3.507 | 3.773 |
| `render-idle` | CPU render | 600 | 1.978 | 2.480 | 2.791 | 5.836 |
| `render-idle` | GPU | 600 | 2.737 | 3.780 | 4.054 | 5.665 |
| `gameplay-neutral-tick` | 전체 tick 호출 | 100 | 4.443 | 5.832 | 6.446 | 9.492 |
| `gameplay-neutral-tick` | 실제 프레임 간격 | 600 | 3.207 | 6.914 | 8.091 | 11.779 |
| `gameplay-neutral-tick` | CPU main | 600 | 2.377 | 6.908 | 8.086 | 11.771 |
| `gameplay-neutral-tick` | CPU render | 600 | 2.021 | 2.488 | 2.682 | 2.912 |
| `gameplay-neutral-tick` | GPU | 600 | 2.742 | 3.854 | 4.172 | 4.551 |

Draw Calls 중앙값은 양쪽 구간 모두 2,042다. gameplay 구간의 frame p95 증가가 CPU main p95 증가와 함께 관측됐지만, 이 합산 계측만으로 시뮬레이션/Presenter/그 외 CPU 작업의 원인별 기여를 확정하지 않는다. 실행 전체의 성능 예산 판정은 `NOT_CONFIGURED`다.

### 프레임 GC 별도 진단

Release형 Player는 `GC Allocated In Frame` counter가 unavailable이며 유효 GC 표본은 0개다. 이 값은 할당 0을 뜻하지 않는다. 추가 진단에서는 동일 capture build method의 옵션만 일시적으로 `BuildOptions.Development`로 바꿔 기존 lane을 실행했다. 종료 후 Editor build source를 byte-for-byte 복원했다. Development의 CPU/GPU 수치는 위 표에 섞지 않았다.

Development에서 GC counter는 available이며 각 구간 600/600 표본을 확보했다. 아래는 게임·엔진·측정 probe를 포함한 **프레임당 할당 바이트** 진단값이다. Release형 Player의 정확한 할당량으로 일반화하지 않는다.

| 구간 | 중앙값, bytes/frame | p95 | p99 | 최대 |
|---|---|---|---|---|
| `render-idle` | 2,773 | 2,845 | 2,845 | 3,465 |
| `gameplay-neutral-tick` | 2,765 | 4,473,581 | 4,473,608 | 4,484,798 |

gameplay 구간의 GC p95는 약 4.27 MiB/frame이다. 매 6프레임마다 tick을 실행한 이 고정 workload의 Development 진단 결과이며, 이전 microbenchmark의 GC event count와 단위·범위가 다르다. 할당 원인별 분해는 수행하지 않았다.

### 실행 상태와 증거

- 첫 실행은 측정 전 임시 save filename이 261자에 도달하는 경로 오류로 bootstrap에 실패했다. 이 실행은 결과에서 제외했다.
- 재실행은 `TEMP`와 `TMP`를 `D:\J2M\evidence\t`로 지정하고 WSLENV `TEMP/p:TMP/p`로 전달했다. Windows GetTempPath 반영을 확인했고 동일 capture identity 격리 정책을 유지해 Player bootstrap이 성공했다.
- Release형 runtime marker는 `GAMEPLAY_PERFORMANCE:PASS`, `performance-admission-report.json`은 `ADMITTED`다. 전체 lane은 별도 Cleanup 검증의 `ALLOCATION_COUNTER_PROBE_INVALID: expectedAtLeast=4096 observed=0` 때문에 `HOLD_CLEANUP_ADMISSION`으로 종료했다. CPU/GPU 측정의 admission과 전체 lane 상태를 구별한다.
- Development 진단은 runtime marker PASS지만 GPU 유효 표본이 idle 599/600이라 `REJECTED_SAMPLE_COUNT`, 전체 lane은 `HOLD_PERFORMANCE_ADMISSION`이다. GC 표본은 두 구간 모두 600개이며 위 값은 해당 진단 원자료로만 보고한다. 공식 Release 성능 admission을 받은 데이터로 취급하지 않는다.
- 임시 build option 변경을 복원했고 측정 전후 tracked diff, 대상 소스 hash, build guard 복원을 확인했다. runtime·Prefab·Scene의 지속 변경은 없다. core/UI/full 회귀 재실행은 소스가 바뀌지 않아 수행하지 않았으며 기존 검증 기록을 유지한다.

재현 명령(증거/build root는 실행별 isolated leaf를 lane이 생성):

```bash
TEMP=/mnt/d/J2M/evidence/t TMP=/mnt/d/J2M/evidence/t \
WSLENV="${WSLENV:+$WSLENV:}TEMP/p:TMP/p" \
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/20260910-driver-cache-whole-player/runner-2 \
GAMEPLAY_PERFORMANCE_EVIDENCE_ROOT=/mnt/d/J2M/evidence/20260910-driver-cache-whole-player/captures \
GAMEPLAY_PERFORMANCE_BUILD_ROOT=/mnt/d/J2M/builds/20260910-driver-cache-whole-player \
./run_tests.sh gameplay-performance
```

증거 루트는 `/mnt/d/J2M/evidence/20260910-driver-cache-whole-player/`다. `summary.json`, `release-summary.json`, `source-hashes.json`, `before.patch`, `run-1-console.log`, `run-2-console.log`, `gc-diagnostic-console.log`를 보관했다. 성공 Release capture는 `captures/cleanup-s3a-20260910T104709Z-415b9fca-0be1-45d2-8908-2d37d66a1155/`, Development GC 진단은 `gc-diagnostic/cleanup-s3a-20260910T105120Z-a985116e-b62b-42c0-91bc-ea97bc8417ea/`다. 각 디렉터리의 metrics, build/runtime log, preflight/artifact manifest, admission report에 소스·Player payload identity가 기록돼 있다. `run_gc_diagnostic.py`, 원본/진단용 build source와 hash도 함께 보관했다.

허용 결과는 이 stage·입력·해상도·장비에서 얻은 최종본 전체 tick/프레임 수치와 별도 Development GC 진단이다. 전체 게임/모든 stage의 성능 보장, 최적화 전후 전체 tick 개선률, 전체 lane PASS나 Cleanup admission 해결은 주장하지 않는다. 기존 기능 실패 8건과 Cleanup 할당 계측 문제는 별도 backlog다.

## 후반 Stage 재측정 (2026-09-10)

다른 worktree의 `Docs/Testing/Gameplay-Test-Automation-Guide.md:557`에서 후반 측정 대상이 `stage-4-2`, `stage-4-3`임을 재확인했다. 해당 문서 원본은 `/mnt/d/J2M/worktrees/ui-callback-attribution/Docs/Testing/Gameplay-Test-Automation-Guide.md`다. `stage-4-3`은 worst-case acceptance, `stage-4-2`는 비교·비용 귀속 workload이고 기존 `stage-1-1`은 historical continuity lane이다. 앞 절의 1-1 결과만으로 후반 Stage 검증을 대신할 수 없다.

현재 작업트리에는 `gameplay-late-stage-performance` lane이 없어, 현재 캐시 수정본을 유지한 채 기존 `./run_tests.sh gameplay-performance`의 build/runtime `--capture-stage` 두 인자만 임시 변경했다. 실행 후 runner를 byte-for-byte 복원했다. 다른 worktree의 FORMAL lane을 실행하거나 공식 paired acceptance를 충족한 결과가 아니다. 최적화 전후 비교가 아닌 최종본의 Stage별 3회 반복 측정이다.

ReleaseLikeCapture Windows Mono, Unity 6000.3.11f1, i5-13500 / RTX 4060 Ti, PC 품질, D3D11, 1920×1080, VSync 0, targetFrameRate -1, warmup 120프레임, idle/gameplay 각각 600프레임, neutral 입력, Tick interval 6을 사용했다. 실행 순서는 `4-2, 4-3, 4-3, 4-2, 4-2, 4-3`이며 각각 별도 빌드·Player 프로세스다. 모든 runtime log에서 요청 Stage와 PASS marker를 확인했고 매 실행 100 Tick 및 각 구간 CPU main/render 600표본을 확보했다.

아래는 gameplay-neutral-tick 구간의 ms 값이다. 중앙값·p95 열은 **각 실행 통계의 3회 중앙값**이며 pooled percentile이 아니다. GPU 표본 부족으로 admission이 거부된 실행도 포함한 **진단 집계**다. 실패·느린 실행을 제외하거나 교체하지 않았다.

| Stage | 지표 | 중앙값 | p95 | 실행별 p95 범위 |
|---|---|---:|---:|---|
| 4-2 | 전체 Tick | 5.281 | 7.327 | 7.315–7.393 |
| 4-2 | 프레임 간격 | 2.398 | 8.129 | 8.095–8.170 |
| 4-2 | CPU main | 2.229 | 8.121 | 8.091–8.162 |
| 4-2 | CPU render | 1.333 | 1.694 | 1.651–1.701 |
| 4-2 | GPU | 2.127 | 2.738 | 2.678–2.963 |
| 4-3 | 전체 Tick | 6.378 | 8.226 | 7.969–8.841 |
| 4-3 | 프레임 간격 | 2.705 | 9.276 | 9.163–9.352 |
| 4-3 | CPU main | 2.323 | 9.269 | 9.160–9.345 |
| 4-3 | CPU render | 1.605 | 1.986 | 1.935–2.086 |
| 4-3 | GPU | 2.089 | 2.622 | 2.588–2.641 |

Idle 프레임 간격 p95의 3회 중앙값은 4-2 **3.001ms**, 4-3 **2.955ms**다. 전체 Tick은 앞 절과 동일하게 동기 Presenter까지 포함한다. Stage 간 수치 차이는 서로 다른 workload의 차이이며 캐시 최적화 효과로 해석하지 않는다.

| 실행 | Stage | GPU 표본 idle/gameplay | 성능 admission | 전체 lane |
|---|---|---|---|---|
| 1 | 4-2 | 595/599 | REJECTED_SAMPLE_COUNT | HOLD_PERFORMANCE_ADMISSION |
| 2 | 4-3 | 600/600 | ADMITTED | HOLD_CLEANUP_ADMISSION |
| 3 | 4-3 | 599/600 | REJECTED_SAMPLE_COUNT | HOLD_PERFORMANCE_ADMISSION |
| 4 | 4-2 | 596/596 | REJECTED_SAMPLE_COUNT | HOLD_PERFORMANCE_ADMISSION |
| 5 | 4-2 | 600/598 | REJECTED_SAMPLE_COUNT | HOLD_PERFORMANCE_ADMISSION |
| 6 | 4-3 | 600/600 | ADMITTED | HOLD_CLEANUP_ADMISSION |

GPU 부족 판정은 기존 validator의 phase별 600개 기준을 그대로 적용했다. 통과한 실행 2·6의 전체 lane 보류 원인은 기존 `ALLOCATION_COUNTER_PROBE_INVALID: expectedAtLeast=4096 observed=0`이다. Release 프레임 GC counter는 모든 실행에서 unavailable이며 0할당을 뜻하지 않는다. 별도 Development GC 재측정은 하지 않았다.

증거는 `/mnt/d/J2M/evidence/20260910-driver-cache-late-stages/`에 보존했다. `run_capture.py`, Stage별 임시 runner와 원본, `runs.json`, 실행별 `capture-*` metrics/admission/build/runtime log, `analyze.py`, `summary.json`, `report.md`, `restoration-verification.json`이 있다. 원본 runner 복원과 측정 전후 전체 tracked diff 동일성을 확인했다. 소스 hash 수집 완료 시점이 첫 runner 변경 이후였으므로 runner는 별도 원본과 대조했고 나머지 31개 파일은 hash가 일치했다. runtime 변경 없이 측정했으므로 core/UI/full 회귀는 재실행하지 않았다. 이 결과는 후반 Stage의 최종본 진단 측정이며 공식 후반 lane PASS나 최적화 전후 개선률을 주장하지 않는다.

## 후반 Stage 최적화 전 대조 측정 (2026-09-10)

앞 절 이후 사용자 요청으로 원본 대조군을 추가 측정했다. `GameplayAnimationSyncCoordinator`, `GameplayEntityPresentationApplier`, `EnemyAnimatorDriver`, `PlayerAnimatorDriver` 4개 runtime 파일을 최초 `before-source-hashes.json`과 일치하는 HEAD 원본으로 임시 교체했다. 사용자 변경인 View factory 등을 포함한 나머지 제품 소스는 최적화 후 캡처 입력과 동일하다. 원본에 없는 API를 참조하는 신규 cache 테스트 2개는 Editor 컴파일 시 임시 preprocessor 제외했으며 Player runtime에는 포함되지 않는다. 종료 후 4개 runtime 파일·테스트·runner를 모두 복원했고, 32개 파일 hash와 측정 전후 전체 tracked diff 동일성을 검증했다.

Stage별 원본 3회, 앞 절 최적화 후 3회로 총 12개 기록을 비교했다. 새 원본 실행 순서는 `4-2, 4-3, 4-3, 4-2, 4-2, 4-3`이다. 기존과 동일한 하드웨어·Unity·OS·quality·해상도·frame cap·warmup120·구간별600프레임·neutral 입력·interval6/100Tick 설정을 확인했다. **최적화 후를 먼저, 원본을 나중에 측정한 순차 비교**이며 interleaved paired 실험이나 FORMAL late-stage lane 검증이 아니다.

아래는 gameplay 구간의 **진단 집계**다. 단위 ms, 전·후는 실행별 통계의 3회 중앙값, 변화율은 `(후/전−1)×100`이다. GPU 표본 부족으로 거부된 실행도 포함하며 실패·느린 실행을 삭제하거나 교체하지 않았다.

| Stage | 지표 | 최적화 전 | 최적화 후 | 변화 | 실행별 전 범위 | 실행별 후 범위 |
|---|---|---:|---:|---:|---|---|
| 4-2 | 전체 Tick 중앙값 | 5.397 | 5.281 | −2.15% | 5.354–5.480 | 5.274–5.297 |
| 4-2 | 전체 Tick p95 | 7.473 | 7.327 | −1.96% | 7.400–8.031 | 7.315–7.393 |
| 4-2 | 프레임 p95 | 8.354 | 8.129 | −2.69% | 8.304–8.575 | 8.095–8.170 |
| 4-2 | CPU main p95 | 8.347 | 8.121 | −2.71% | 8.299–8.570 | 8.091–8.162 |
| 4-3 | 전체 Tick 중앙값 | 6.764 | 6.378 | −5.70% | 6.503–6.872 | 6.333–6.545 |
| 4-3 | 전체 Tick p95 | 8.894 | 8.226 | −7.51% | 8.801–8.908 | 7.969–8.841 |
| 4-3 | 프레임 p95 | 9.832 | 9.276 | −5.66% | 9.389–10.116 | 9.163–9.352 |
| 4-3 | CPU main p95 | 9.827 | 9.269 | −5.68% | 9.384–10.111 | 9.160–9.345 |

사전에 기록한 진단 기준은 5% 이상 변화와 반복 범위 비중첩이다. 4-2 Tick·프레임 변화는 작아 미확정이고, 4-3 Tick 중앙값/p95는 전후 범위가 겹쳐 미확정이다. 4-3 프레임·CPU main p95는 낮은 값이 관측됐지만 인과적 개선을 확정하지 않는다. Tick을 실행하지 않는 4-3 idle에서도 프레임 p95가 **3.267→2.955ms(−9.57%)**, CPU main p95가 **3.113→2.794ms(−10.24%)**로 낮아졌다. 따라서 실행 시간대/환경 또는 비-Tick 표시 비용과 캐시 효과를 이 순차 비교만으로 분리할 수 없다. 특정 OS/driver 원인을 확정하지 않으며, 기존 microbenchmark의 약 80% 조회 비용 감소를 전체 Tick 개선률로 대입하지 않는다.

원본 대조군은 4-3 세 실행이 성능 `ADMITTED`, 4-2 세 실행은 GPU 표본 부족으로 `REJECTED_SAMPLE_COUNT`다. 원본의 GPU idle/gameplay 표본은 실행 순서대로 `599/599, 600/600, 600/600, 594/594, 589/583, 600/600`이다. 양 소스 총 12회 중 성능 승인 5회·표본 거부 7회이며, 승인된 실행도 기존 Cleanup `ALLOCATION_COUNTER_PROBE_INVALID: expectedAtLeast=4096 observed=0` 때문에 전체 lane은 HOLD다. 모든 실행의 CPU main/render는 구간별600표본, gameplay Tick은100표본을 확보했다. Release GC counter는 unavailable이어서 GC 바이트 전후 비교는 수행하지 않았다.

새 증거 루트는 `/mnt/d/J2M/evidence/20260910-driver-cache-late-stage-control/`다. `comparison-report.md`, `comparison.json`, `protocol.md`, `run_control.py`, `analyze.py`, `compare.py`, 각 실행 metrics/admission/build/runtime log, `baseline/`, `optimized/`, `restore-backup/`, `source-hashes.before.json`, `optimized-input-verification.json`, `restoration-verification.json`을 보존했다. 기존 최적화 후 원자료 hash도 비교 시 재검증했다. 임시 변경 복원 뒤 문서만 추가했으므로 core/UI/full 행동 회귀는 재실행하지 않았다. 현재 결론은 **대조 측정에서 감소 방향을 관측했으나, 전체 Tick·프레임의 캐시 기인 개선 확정은 보류**다.

## A/B·B/A 교차 측정 (2026-09-10)

사용자 요청으로 원본 A와 최적화 후 B를 Stage별 **AB → BA → BA → AB** 네 쌍씩 새로 측정했다. 총16회·8쌍이며 이전 순차 측정값은 이번 통계에 포함하지 않았다. Stage 실행 순서도 블록별로 바꿨다. 각 쌍은 같은 Stage의 두 변형을 인접 실행했으며, 각각 기존 worktree의 `./run_tests.sh gameplay-performance`로 별도 Player를 빌드·실행했다. 별도 FORMAL late-stage lane을 실행한 것은 아니다.

A는 최초 baseline hash와 일치하는 4개 runtime 원본, B는 최종 캐시·View 교체 상태 복원 수정본이다. A/B 사이 제품 소스 차이가 이 4개 파일뿐임을 각 쌍의 hash로 확인했다. 신규 cache 테스트 2개의 Editor 컴파일 제외는 A/B 양쪽에 동일하게 적용했고 Player runtime에는 포함되지 않는다. 나머지 사용자 변경은 유지했다. 실행 전후 32개 파일 hash와 전체 tracked diff가 동일하며 임시 코드·테스트·runner 변경은 모두 복원했다.

조건은 동일한 Unity6000.3.11f1·Windows Mono ReleaseLikeCapture·i5-13500/RTX4060Ti·PC 품질·D3D11·1920×1080·VSync0·targetFrameRate−1·warmup120·phase별600프레임·neutral 입력·interval6/100Tick이다. 모든 쌍에서 실제 설정·Stage·runtime PASS 및 source identity를 검증했다. 16회 모두 구간별 CPU main/render600개와 gameplay Tick100개를 확보했다. CPU 지표별 비교 조건 충족과 전체 capture admission을 구별했다.

아래 변화율은 각 쌍의 `(B/A−1)×100`을 먼저 계산한 뒤 평균·중앙값으로 요약한 값이다. 음수는 B가 빠름, 양수는 B가 느림을 뜻한다. 기존의 임의 5% 기준으로 유효성이나 통계적 유의성을 판정하지 않았고, 실행을 값에 따라 제외·교체하지 않았다.

| Stage | 지표 | 쌍별 변화율 평균 | 쌍별 변화율 중앙값 | B가 빠른 쌍 |
|---|---|---:|---:|---|
| 4-2 | 전체 Tick 중앙값 | -7.33% | -0.28% | 2/4 |
| 4-2 | 전체 Tick p95 | -8.20% | +1.59% | 2/4 |
| 4-2 | 프레임 p95 | -7.05% | +0.46% | 2/4 |
| 4-2 | CPU main p95 | -7.02% | +0.48% | 2/4 |
| 4-3 | 전체 Tick 중앙값 | +2.60% | +2.89% | 1/4 |
| 4-3 | 전체 Tick p95 | +2.48% | +0.09% | 2/4 |
| 4-3 | 프레임 p95 | +3.14% | +2.35% | 1/4 |
| 4-3 | CPU main p95 | +3.13% | +2.31% | 1/4 |

실제 쌍별 B−A 차이(ms):

| Stage | 쌍 | 순서 | Tick p95 | 프레임 p95 | idle 프레임 p95 |
|---|---|---|---:|---:|---:|
| 4-2 | 1 | AB | +0.502 | +0.314 | -0.133 |
| 4-3 | 1 | AB | -0.588 | -0.052 | -0.273 |
| 4-2 | 2 | BA | -5.759 | -4.828 | -7.051 |
| 4-3 | 2 | BA | -0.079 | +0.006 | -0.157 |
| 4-2 | 3 | BA | -0.241 | -0.246 | +0.093 |
| 4-3 | 3 | BA | +1.534 | +0.834 | -0.198 |
| 4-2 | 4 | AB | +0.474 | +0.604 | -0.073 |
| 4-3 | 4 | AB | +0.100 | +0.462 | +1.000 |

**결론: 전체 Tick·프레임의 일관된 개선은 이번 교차 측정에서 재현되지 않았다.** 두 Stage 모두 Tick p95가 빨라진 쌍과 느려진 쌍은 2:2다. 4-2 프레임도 2:2이며 4-3 프레임은 B가 느린 쌍이 3개다. 이 판단은 GPU 부족만을 이유로 보류한 것이 아니라, 완전한 Tick·CPU 표본에서도 변화 방향이 섞인 데 근거한다.

4-2 평균 감소는 pair2의 A 지연에 크게 영향을 받았다. 해당 실행은 idle도 크게 지연됐으며 원자료를 제외하지 않았다. 쌍별 변화율 중앙값에서는 Tick p95 +1.59%, 프레임 p95 +0.46%로 감소가 나타나지 않았다. 4-3은 Tick p95 +0.09%, 프레임 p95 +2.35%다. Tick p95 평균은 두 Stage 모두 AB와 BA에서 방향이 달랐다. 작은 네 쌍의 결과만으로 성능 회귀나 통계적 유의성을 확정하지도 않는다.

기존 Editor 동일 workload의 드라이버 조회 비용 약80–81% 감소는 국소 최적화의 실증 근거로 유지한다. 앞선 순차 측정의 전체 Tick·프레임 감소율을 캐시의 재현된 개선 효과로 인용하지 않는다. 이번 결과는 그 원인 귀속에 제한이 있음을 추가로 보여준다.

전체 performance admission은 6회 ADMITTED, 10회 REJECTED_SAMPLE_COUNT다. 완전한 GPU 표본을 가진 pair는 4-3의 1·2·4번 세 쌍이다. 기존 validator를 완화하지 않았고, ADMITTED 캡처도 Cleanup allocation 양성 대조군(4096 대비0) 때문에 전체 lane은 HOLD다. CPU endpoint 비교 조건은 이 전체 판정을 대체하지 않는다. Release GC는 unavailable이므로 할당량 개선을 주장하지 않는다.

증거 루트: `/mnt/d/J2M/evidence/20260910-driver-cache-paired/`. `protocol.md`, `schedule.json`, `runs.json`, `run_pairs.py`, `analyze_pairs.py`, `paired-summary.json`, `paired-report.md`, 실행별 source hash/metrics/admission/build/runtime log, `original/`, `baseline/`, `restoration-verification.json`을 보존했다. `paired-summary.json`에는 모든 phase의 median/p95/p99/max, 각 쌍의 차이, AB/BA별 평균과 idle 차이도 있다. phase p95 차이의 차이는 진단용 요약이며 배타적 Tick 비용으로 해석하지 않는다. 영구 runtime 변경 없이 원본 최적화 소스로 복원했으므로 core/UI/full 행동 회귀는 재실행하지 않았다.
