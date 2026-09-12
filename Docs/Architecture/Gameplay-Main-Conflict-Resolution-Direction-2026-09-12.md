# main 통합 충돌 해결 방향과 변경 근거

- 작성일: 2026-09-12 KST
- 상태: **충돌 해결·구현 및 touched 검증 완료, broad baseline red — 아래 §10 실행 기록 참조**
- 목적: 이후 작업에서도 양쪽 브랜치의 동작을 보존하도록 수정 이유, 구현 경계, 실패 처리, 검증 기준을 유지한다.
- 사용자 요청: main PR 개설 검토 → 서브에이전트의 충돌 원인 및 구체 방안 검토 → 각 수정 이유 설명 → 방향 유지를 위한 문서화.
- §1–9는 최초 검토의 설계 기준과 근거를 보존한다. 실제 구현 상태와 검증 결과는 §10에 별도로 기록한다.

## 1. 사용 방법과 범위

후속 구현자는 이 문서를 먼저 읽고 §3의 보존 계약과 §8의 검증표를 구현·리뷰 기준으로 사용한다. 다른 해결 방식이 필요하면 해당 결정의 이유, 영향받는 계약, 대체 검증을 이 문서에 함께 갱신한다. 테스트 기대값만 낮추거나 파일 전체를 한쪽 버전으로 선택하여 방향을 바꾸지 않는다.

이 문서는 아래 두 리비전의 충돌 해결에 한정된다. [2026-09-11 C/D 최적화 통합 계획](./Gameplay-Optimization-Integration-Plan-2026-09-11.md)과 [그 실행 프롬프트](./Gameplay-Optimization-Integration-Execution-Prompt-2026-09-11.md)는 별도 범위이며, 다른 최적화 브랜치를 이번 작업에 자동 포함하지 않는다. 이 문서는 해당 계획을 실행하거나 대체하지 않는다.

| 구분 | 고정 기준 |
|---|---|
| 현재 브랜치 | `codex/third-party-license-inventory` |
| 현재 HEAD | `3c71601b0cd812cee7c752319fb59eadd9ed667a` |
| 비교 대상 | `origin/main` |
| main 리비전 | `80a203573f2c760b2b3b1ed23bcd4734d64487a8` |
| 공통 조상 | `5d338c54a890bb5225ddda9d769f880846b8f1ca` |

실행 시작 시 원격과 로컬 상태를 다시 확인한다. 기준이 달라지면 새 충돌과 계약을 재검토하며, 이 문서의 리비전을 최신 상태로 오인하지 않는다. 기존 미추적 `Assets/AddressableAssetsData/Windows.meta`는 사용자 작업으로 보존하며 통합 입력에 자동 포함하지 않는다.

## 2. 확인된 충돌과 근거

고정 리비전의 `git merge-tree --write-tree origin/main HEAD` 시험 결과는 3개 파일의 텍스트 충돌이다. 실제 브랜치/index/작업파일 병합은 수행하지 않았다.

| 파일 | 현재 브랜치 변경 | main 변경 | 해결 방향과 이유 |
|---|---|---|---|
| `EnemyAnimatorDriver.cs` | `261f2df07`: View 교체 시 의미 상태·Utility 진행률·Death 복원 | `2fb4a2d4e`, `fd1d40f9b`: Cue binding 도입 및 legacy authoring 제거 | 새 binding 구조에 교체 복원을 이식한다. 옛 helper를 되살리면 새 구조를 우회하고, 복원 기능을 삭제하면 기존 교체 동작이 사라진다. |
| `GameplayAnimationSyncCoordinator.cs` | `261f2df07`: 드라이버 캐시와 정리 helper, 교체 상태 보존 | `d0092688c`: 정규화 실패에도 정리 완료; `2fb4a2d4e`: Utility track의 Cue 전환 | 캐시·예외 안전성·Cue를 결합한다. 충돌 표시 밖의 `ResolveDrivers`에도 옛 `track.Phase` 호출이 남을 수 있다. |
| `run_tests.sh` | `e28148cae`: usage에 `cleanup-s3-capture-smoke` 추가 | `58747b7b9`: `climate-glyph-update`를 `kbo-glyph-update`로 교체 | 두 lane 이름을 함께 유지한다. 같은 도움말 줄을 고친 텍스트 충돌이며 기능은 독립적이다. |

코드 위치는 다음 경로를 기준으로 각 리비전에서 확인한다. main에만 존재하는 파일은 현재 작업트리에 없을 수 있으므로 `git show <고정 SHA>:<경로>`로 확인한다.

- `Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyAnimatorDriver.cs`
- `Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyAnimationBindingTypes.cs`
- `Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyAnimationBindingAuthoring.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/SummonedEnemyPresentationResolver.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/GameplayDriverCachePlayModeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/EnemyAnimationSparseBindingRuntimeScenarioTests.cs`

## 3. 유지할 계약과 선택하지 않은 대안

1. main의 일반 resync는 Trigger 전용 cue를 임의로 재발행하지 않는다. `TriggerOnlyCues_AreNotGeneralResyncTargets_AndAreNotRepublished`를 유지한다.
2. 일반 Jump resync와 topology suspend 복원은 서로 다른 진행률 규칙을 유지한다. `NewBinding_JumpTopologyPreservesNormalizedTime_WhileGeneralResyncReenters`를 변경하지 않는다.
3. View 교체는 같은 엔티티의 표시 교체다. 의미 상태와 Utility 남은 시간을 보존하고, one-shot 신호와 scale pulse 진행을 재발행·이전하지 않는다.
4. 실제 Release/Reset은 새 수명 경계다. 캐시·hold·track·복구용 상태가 같은 ID의 다음 수명에 남지 않는다.
5. sparse binding에 cue가 없으면 legacy state 이름으로 추정하지 않는다.
6. authoritative simulation, Tick 순서, WorldState 쓰기, audio/VFX 이벤트 발행 정책은 이번 표시 복원 설계에서 변경하지 않는다.

| 선택하지 않은 대안 | 이유 |
|---|---|
| 파일 전체에 ours/theirs 선택 | 한쪽의 캐시·복원 또는 binding·정리 계약이 사라진다. |
| Trigger를 State dispatch로 일괄 변경 | 정상 controller 전이까지 바뀌므로 교체 문제보다 넓은 동작 변경이다. |
| Trigger 이름을 복원 state 이름으로 사용 | 이름이 같다는 계약이 없으며 sparse binding을 우회한다. |
| 일반 resync에서 모든 Trigger 복원 허용 | main의 명시적 테스트와 일반 재동기화 동작을 바꾼다. |
| 기존 Animator snapshot만 복사 | pause 중 Death 입력 또는 비활성 replacement에 Death가 도착한 경우 캡처할 Death state 자체가 없을 수 있다. |
| 기존 DrSaturn 복원 테스트 완화 | 현재 브랜치가 제공하던 실제 Death/Utility 시각 복원을 잃은 것을 숨긴다. |

## 4. 교체 전용 Cue 복원

### 4.1 API 분리와 이유

제안 API는 다음과 같다. 이름은 구현 시 조정할 수 있지만 일반 resync와 교체 복원의 책임 분리는 유지한다.

```csharp
internal void RestorePresentationState(in EnemyViewPresentationState state);
internal EnemyAnimationDispatchResult RestorePresentationCue(
    EnemyAnimationCue cue, float normalizedTime = 0f);
internal void UpdatePendingUtilityPresentationCue(
    EnemyAnimationCue cue, float normalizedTime);
```

일반 resync는 현재 Animator를 다시 맞추고, 교체 복원은 새 Animator에 기존 표시 상태를 전달한다. 두 진입점을 분리해야 trigger 재발행 금지와 교체 진행률 보존을 동시에 지킬 수 있다.

`RestorePresentationState`는 의미 속성과 `LastPresentationState`를 옮기고 이전 pending을 초기화한다. 이전 one-shot flags가 값 carrier에 포함되어도 dispatch 입력으로 다시 소비하지 않는다. binding cache나 폐기된 직렬화 필드는 복원하지 않는다. Death 등 지속 상태의 교체 복원은 교체 전용 Cue 경로로 전달한다.

`ApplyPresentationCueTiming(track.Cue)`를 유지하고, `ApplyAnimatorTimingForCue` 등 main의 Cue timing API를 사용한다. 옛 `_pendingCrossFadeStateName`, `_pendingCrossFadeRequiresOverride`, `ResolveStateName`, `ResolveAnimatorStateHash`, phase 기반 복원 호출은 이식 완료 시 잔존 여부를 확인한다. 일반 phase vocabulary 전체를 삭제한다는 의미는 아니다.

### 4.2 선택적 replacementStateName

serialized binding과 runtime binding에 `replacementStateName` / `ReplacementStateName`을 추가한다. 기본값은 비어 있음이며 Trigger `UtilityWindup`, `UtilityRecovery`, `Death`에만 선택적으로 허용하는 안을 기준으로 한다.

| 교체 대상 binding | 복원 state 결정 | 이유 |
|---|---|---|
| State | `TargetName` | 이미 명시된 state 계약을 재사용한다. |
| Trigger JumpAirborne | `SustainedStateName` | main의 별도 sustained-state 계약을 유지한다. |
| Trigger UtilityWindup/UtilityRecovery/Death | 명시적인 `ReplacementStateName` | Trigger 전이를 재발행하지 않고 복원 목적지만 선언한다. |
| cue 없음 또는 지원하지 않는 Trigger | `Unsupported` | 이름 추정이나 누락 cue의 legacy fallback을 막는다. |

`TryResolveRestorableState` / `TryRestoreCueState`의 **일반 resync eligibility는 확장하지 않는다.** metadata가 있어도 일반 Trigger resync는 기존 결과를 유지해야 한다.

main의 `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_DrSaturn.prefab`은 아래 세 cue가 모두 Trigger이며 sustained state는 비어 있다. 기존 HEAD PlayMode 테스트는 교체 후 실제 state와 남은 진행률을 요구한다.

| Cue | 기존 primary target | 추가할 replacementStateName |
|---|---|---|
| UtilityWindup (`50`) | Windup | Windup |
| UtilityRecovery (`51`) | Recover | Recover |
| Death (`91`) | Death | Death |

이 Prefab 변경의 목적은 교체 복원 목적지 명시다. Primary dispatch, controller 전이, clip, duration은 유지한다. Hit나 다른 Prefab을 자동 변경하지 않는다. 다른 binding에 대한 시각 복원 동등성은 별도 검증 없이 주장하지 않는다.

`EnemyAnimationBindingAuthoring`의 snapshot 전달·허용 cue/mode 검증, `EnemyAnimationBindingAuthoringEditor`의 조건부 필드 표시·새 행 초기화, `EnemyAnimationControllerBindingValidator`의 layer 0 state 해석 검증을 함께 반영한다. migration 도구가 binding을 재작성할 때 새 값을 잃지 않는지도 확인한다. 이유는 직렬화된 값이 runtime까지 전달되고 잘못된 목적지가 교체 순간까지 잠복하지 않도록 하기 위해서다.

### 4.3 Pending 수명

교체 pending은 `HasValue`, `Cue`, `StateName`, `NormalizedTime`, `FailOnUnresolvedState`를 보관한다. main의 일반 `_pendingStateCommand`와 CrossFade 계약은 유지하되 두 예약이 서로 덮어쓰는 조건을 테스트로 명시한다.

| 상황 | 처리 | 이유 |
|---|---|---|
| 활성 Animator로 교체 | strict state 해석 후 지정 진행률로 `Play` | cross-fade 시작점으로 돌아가 진행률을 잃지 않는다. |
| 비활성 또는 pause | 교체 pending 유지 | 재생 불가능한 순간에 요청이 소실되지 않는다. |
| Utility 진행 | 정확한 Utility Cue와 진행률 갱신 | 재개 시 오래된 위치를 재생하지 않는다. |
| Utility 만료 | Utility pending만 취소 | 끝난 동작의 뒤늦은 재생을 막으며 Death pending은 보존한다. |
| 교체 pending 중 Death | Utility를 취소하고 명시된 Death 복원 우선 | 사망 후 Windup/Recover로 되돌아가는 것을 막는다. |
| 새 실제 재생 명령 | 대체되는 교체 pending 취소 | 옛 예약이 최신 명령을 덮어쓰지 않는다. |
| 새 교체 복원 | 이전 일반 pending 제거 | 이전 View 수명의 예약을 소비하지 않는다. |
| 교체 pending 소비 성공 | 같은 resync 호출에서 조기 반환 | 직후 일반 resync가 진행률을 초기화하지 않는다. |
| 평상시 상태 동기화 | 복원을 반복 예약하지 않음 | 매 프레임 애니메이션 재시작을 막는다. |

Utility 교체 진행률은 `Clamp01`을 적용한다. 기존 Jump topology의 normalized time 규칙에 이를 일괄 적용하지 않는다. Death는 남아 있는 Airborne/Glide carrier보다 우선하고 hidden-airborne suppression도 Death를 가리지 않아야 한다.

## 5. 캐시 교체, 해제 및 실패 복구

### 5.1 정리 경계

`RemoveDriverBindings(entityId)`는 pulse 정규화를 시도하고, `finally`에서 Enemy/Player/Pulse 실행용 dictionary와 `_driverCacheByEntityId`를 모두 제거한다. 이 helper는 entity track/hold를 지우지 않는다.

이유: 정규화 실패 후 cache entry만 남으면 다음 조회가 정상 cache hit으로 오인한다. 반대로 여기서 entity 상태까지 지우면 View 교체가 수명 종료로 처리된다.

`ReleaseEntity(entityId)`는 위 helper를 호출하고, 바깥 `finally`에서 Utility track, Enemy 의미 상태, contact-delayed death, Player hold/death/의미 상태 및 실패 snapshot을 제거한다. 원래 예외를 숨기지 않으며 두 번째 release는 안전해야 한다.

### 5.2 ResolveDrivers 교체 순서

1. 기존 적용 Enemy/Player 상태와 현재 Utility track을 선택한다. driver가 없으면 mapper 또는 §5.3 실패 snapshot을 사용한다.
2. 새 View의 세 component를 지역 변수로 조회한다.
3. 후보 driver에 지속 상태와 현재 `track.Cue`, `ElapsedSeconds / DurationSeconds`를 복원한다. 아직 cache/maps에는 게시하지 않는다.
4. 준비 성공 후 이전 binding을 제거한다. 정규화 예외는 보관하고 helper의 `finally` 정리는 끝낸다.
5. 준비된 후보를 cache/maps에 게시한다. 이 구간에는 사용자 callback을 넣지 않는다. 새 Player가 없으면 Player 상태를 정리한다.
6. 실패 snapshot을 제거한 후 보관한 정규화 예외가 있으면 `ExceptionDispatchInfo`로 원래 오류를 전달한다.

| 실패 위치 | 결과와 이유 |
|---|---|
| component 조회·후보 복원 | 새 cache를 게시하지 않는다. 다음 시도가 cache hit으로 생략되지 않게 한다. 후보 Animator의 부분 변경까지 rollback한다는 보장은 하지 않는다. |
| 이전 pulse 정규화 | 이전 maps/cache 제거 후 준비된 후보 게시를 완료하고 원래 오류를 전달한다. 사용 가능한 새 View를 잃거나 실패를 숨기지 않는다. |

같은 View의 cache hit, frozen negative component 결과, destroyed-positive retirement는 유지한다. missing View에서는 binding/cache 정리를 보장하고 Player 상태를 정리하되 실제 Release와 달리 Enemy Utility 수명을 임의 종료하지 않는다.

### 5.3 복원 실패 후 기존 View 파괴

소환 resolver는 후보 복원이 실패해도 `finally`에서 old owned View를 파괴할 수 있다. old cache/maps만 유지하는 초기 제안으로는 이후 재시도의 Death 상태를 보존할 수 없다.

후보 준비 실패 catch에서만 lazy `Dictionary<int, ReplacementRestoreSnapshot>`에 최신 적용 Enemy/Player **값 상태**를 남기는 안을 사용한다. GameObject 소유권이나 미소비 one-shot 명령은 저장하지 않는다. mapper dictionary는 다음 Build가 덮어쓸 수 있으므로 유일한 복구 저장소로 사용하지 않는다.

- 재시도는 old driver가 없어졌거나 cache entry가 없어도 해당 수명의 실패 snapshot을 사용할 수 있어야 한다.
- Utility 시간은 기존 `_enemyUtilityAnimationTracks`가 계속 소유한다. snapshot에 elapsed time을 복제하지 않는다.
- snapshot 조회는 replacement/miss 경로에 한정하며 정상 cache hit에 추가 작업을 넣지 않는다.
- 실제 Release/Reset 및 복원 성공 시 제거한다. missing View는 복구 상태를 유지할 수 있으나 Player 정리 시 snapshot의 Player 부분도 지운다.
- snapshot과 이후 더 최신 의미 입력의 우선순위는 구현 시 명시하고, 오래된 snapshot이 최신 Death/cancel 또는 새 수명을 덮어쓰지 않는 회귀 테스트를 둔다.

이유: 파괴된 Unity component와 독립적으로 재시도할 상태를 남기면서, Utility 시간의 이중 소유와 정상 경로 비용 증가를 피하기 위해서다.

### 5.4 SummonedEnemyPresentationResolver

`ReleaseOwnedViewIfPresent`의 replacement 선택과 registry 우선순위는 유지하고 animation 처리만 다음처럼 바꾼다.

```text
replacementView 있음: CacheDrivers(entityId, replacementView)
replacementView 없음: ReleaseEntity(entityId)
기존 UnregisterIfMatches / owned View finally 파괴 / 예외 집계 유지
```

이유: replacement가 있는데 먼저 ReleaseEntity를 호출하면 같은 엔티티의 Utility 남은 시간과 지속 상태가 사라진다. 이미 캐시된 replacement는 cache hit으로 보존하고, old View만 캐시돼 있으면 교체 경로에서 상태를 옮긴다. exact-instance unregister와 old owned View 파괴는 누수 방지를 위해 유지한다.

`GameplayEntityPresentationApplier`의 HEAD View identity signature 보정과 main의 legacy death-retention 제거는 모두 보존한다. 자동 병합 성공을 의미 검증 완료로 간주하지 않는다.

## 6. Runner 수정

`print_usage()`에서 `climate-glyph-update`를 제거하고 `kbo-glyph-update`, `cleanup-s3-capture-smoke`를 모두 유지한다.

이유: main의 KBO asset/glyph/NO_MUTATION 검증과 HEAD의 S3 capture/evidence/fail-closed 종료 기능이 각각 필요하다. runner 전체를 한쪽으로 선택하지 않는다. 실행부는 자동 병합 결과를 검사하고 관련 회귀 테스트로 검증한다.

## 7. 구현 순서와 저장 위치

1. 상태·diff·remote 기준을 재확인하고 사용자 변경을 보존한다. 기준이 변경됐으면 §2의 충돌과 §3의 계약을 다시 확인한다.
2. 새 통합 worktree가 필요하면 `j2m-worktree-add`를 사용하여 `/mnt/d/J2M/worktrees` 아래 생성한다. 직접 `git worktree add`는 사용하지 않는다. D 여유 30 GiB 이상, C 10 GiB 미만 경고, `j2m-worktree-audit` 및 private Library를 확인한다.
3. 실행 요청 범위에 따라 고정 HEAD와 main을 결합하고 runner 충돌을 해결한다. 문서화 자체는 실제 merge 실행을 뜻하지 않는다.
4. binding metadata·snapshot·validator와 focused 테스트를 작성하고, Driver의 교체 전용 API/pending을 구현한다.
5. Coordinator의 Cue 전환·교체/해제·실패 snapshot과 resolver 분기를 구현한다.
6. DrSaturn의 세 복원 목적지를 설정한다. 기존 GUID와 관련 `.meta`를 유지하고 asset 변경 목적 및 Editor/PlayMode 증거를 기록한다.
7. §8의 검증을 통합 worktree에서 실행하고 실패를 분류한다. 최종 변경 후 해당 결과를 재확인한다.

새 evidence는 `/mnt/d/J2M/evidence`, build는 `/mnt/d/J2M/builds` 아래 실행별 디렉터리에 둔다. 검증 전 resolved project path가 `/mnt/d/J2M/worktrees/`로 시작하는지 확인한다. 기존 C worktree는 이동·삭제하지 않는다.

`TEST_RESULTS_ROOT`, `TEST_LOG_ROOT`, `CAPTURE_ROOT`는 evidence 쪽으로, 외부 설정 변수 `PLAYER_BUILD_ROOT`, `GAMEPLAY_PERFORMANCE_BUILD_ROOT`, `CLEANUP_S3_CAPTURE_SMOKE_BUILD_ROOT`는 builds 쪽으로 분리한다. `CLEANUP_S3_CAPTURE_SMOKE_EVIDENCE_ROOT`도 evidence 아래로 지정한다. `CODEX_VALIDATION_ROOT` 하나만 설정해 build가 evidence 아래에 생기지 않도록 실제 runner 설정을 확인한다.

## 8. 검증 기준과 이유

| 검증 | 보존·추가할 내용 | 이유 |
|---|---|---|
| 기존 Driver cache/PlayMode | 실제 DrSaturn 활성·비활성 Death/Utility, 진행률, 만료, Utility 대기 중 Death; JPeter pause/재등장/교체 | 현재 브랜치의 실제 표시 복원과 one-shot 비재발행을 유지한다. |
| 기존 sparse binding | Trigger 일반 resync 금지, missing cue fallback 금지, Jump topology와 일반 재진입 구분, 일반 pending last-write-wins | 교체 기능이 main의 정상 dispatch/resync를 바꾸지 않게 한다. |
| 새 metadata | custom replacement state, 미설정 Trigger Unsupported, 잘못된 cue/mode/state 거부, snapshot·migration 보존 | 이름 추정과 직렬화 누락을 막는다. |
| pending 조합 | pause/비활성 진행률, 만료, Death 우선, 최신 명령 상호 취소 | 재개 시 오래된 상태 재생을 막는다. |
| 교체 실패 | 준비 실패→old owned View 파괴→재시도, direct Death 값 보존, 현재 Utility 남은 시간 사용 | 별도 브랜치 테스트만으로 드러나지 않는 결합 실패다. |
| 캐시 일관성 | 준비 실패 후 재시도, 정규화 실패 후 새 후보 게시와 원래 오류 전달 | 부분 게시와 잘못된 cache hit을 막는다. |
| 수명 종료 | 정규화 실패에도 전체 정리, 실패 snapshot이 있는 Release/Reset 후 같은 ID 재사용 | 이전 수명 상태의 유출을 막는다. |
| resolver | 이미/아직 캐시되지 않은 replacement 상태 보존, replacement 없음, unregister 실패·batch·teardown | 기존 소유권과 예외 정리를 유지한다. |
| runner | syntax/dry-run/lifecycle/visual interruption | 도움말 수정 뒤 독립 기능과 종료 보호가 유지되는지 확인한다. |

아래는 **후속 구현에서 실행할 명령 예시**이며 문서 작성 시 실행한 테스트가 아니다. evidence/build 환경변수를 설정한 통합 worktree에서 실행한다.

```bash
bash -n run_tests.sh
./run_tests.sh --print-config
./run_tests.sh --dry-run core
./run_tests.sh --dry-run ui
./run_tests.sh --dry-run kbo-glyph-update
./run_tests.sh --dry-run cleanup-s3-capture-smoke
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest \
  Tools.tests.test_gameplay_cleanup_slice3_runner_lifecycle \
  Tools.tests.test_gameplay_cleanup_slice3_evidence_manifest
bash Tools/tests/test_visual_runner_interruption_cleanup.sh

./run_tests.sh full --filter \
'GameplayAnimationDriverCacheTests;GameplayDriverCachePerformanceTests;GameplayDriverCachePlayModeTests;EnemyAnimation;SummonedEnemyPresentationResolver;DefaultEnemyVisualSemanticResolver;PauseProgressionActivationPlayModeTests;GameplayTickPresentationCoordinator_Teardown'

./run_tests.sh core
./run_tests.sh ui
```

lane/checkpoint마다 별도 `TEST_RESULTS_ROOT`를 지정하여 XML 덮어쓰기를 막는다. 각 XML에서 예상 fixture와 양수 실행 수를 확인한다.

필터는 `;` 또는 `,` 구분이다. `EnemyAnimation`은 binding authoring/editor/migration/asset fixture까지 포함하는 의도이며 구현 후 실제 이름을 다시 확인한다. 신규 fixture가 필터 밖이면 추가한다. 전체 합계가 양수여도 특정 fixture가 누락될 수 있으므로 XML에서 각각 확인한다.

`core/ui`만으로 Full 분류인 교체/애니메이션 테스트가 실행되었다고 간주하지 않는다. filtered full 결과와 broad `./run_tests.sh full` 결과를 구분한다. 안정화/통합 전 broad full은 [검증 가이드](../Testing/Gameplay-Test-Automation-Guide.md)에 따라 실행하고 기존 baseline 실패와 touched-cluster 회귀를 분리한다. 결과·실행 수·revision·project path·XML/log·미실행 이유를 남긴다.

## 9. 미해결 사항과 완료 판단

- 위 코드는 설계안이다. 컴파일 및 실제 Unity 동작으로 확인하지 않았다. 현재 문서화 작업에서는 runtime/asset/runner를 변경하지 않아 Unity core/ui/full과 Editor/Player 검증을 실행하지 않는다.
- Normalize 예외를 직접 주입하는 기존 테스트는 검토에서 확인하지 못했다. 재현 가능한 객체 상태를 우선 조사하고, 필요하면 cleanup 경계에 제한된 internal 테스트 seam을 사용한다. 공개 API나 매 프레임 경로를 테스트 때문에 확장하지 않는다.
- `Reset()` 전체의 batch normalization 예외 안전성은 별도 잔여 위험이다. 실패 snapshot clear는 이번 수명 관리에 포함하지만 모든 Reset 실패 복구까지 완료했다고 주장하려면 별도 구현·테스트가 필요하다.
- 실패 snapshot과 새 입력의 우선순위, pending 명령 상호 취소의 정확한 호출 위치는 구현 단계의 집중 검증 대상이다. 설계 설명만으로 이 경계의 안전성을 확정하지 않는다.
- 새 metadata는 DrSaturn 세 cue의 확인된 복원 요구를 지원한다. 다른 Prefab의 Trigger 시각 복원 동등성이나 전체 성능 개선을 자동 주장하지 않는다.
- 충돌 영역 검증만으로 104개 파일의 전체 PR 리뷰가 끝난 것은 아니다. 다른 변경 영역 및 최신 main 추가 변경은 PR 준비 검토에 남는다.

완료 보고에는 이 문서의 각 보존 계약과 검증 항목의 실제 결과를 연결한다. 실패를 숨기기 위해 기존 테스트를 완화하지 않고, 필요한 설계 변경은 이유와 대체 검증을 기록한다. 구현·병합·성능 개선 완료 표시는 해당 실행 증거를 확보한 뒤 갱신한다.


## 10. 2026-09-12 통합 실행 기록

### 범위와 실제 기준

- 원격 `git fetch origin` 후 HEAD `3c71601b0cd812cee7c752319fb59eadd9ed667a`, main `80a203573f2c760b2b3b1ed23bcd4734d64487a8`, 공통 조상 `5d338c54a890bb5225ddda9d769f880846b8f1ca`가 최초 검토와 일치했다. 추가 원격 diff는 없다.
- `j2m-worktree-add main-conflict-20260912 --new codex/main-conflict-resolution-20260912 3c71601b0cd812cee7c752319fb59eadd9ed667a`로 `/mnt/d/J2M/worktrees/main-conflict-20260912`를 생성했다. 생성 전 D 약 775 GiB, C 약 77 GiB 여유 공간 및 생성 전후 `j2m-worktree-audit` PASS를 확인했다. Library는 다른 worktree와 공유하지 않는다.
- 새 worktree에서 `git merge --no-commit --no-ff 80a203573f2c760b2b3b1ed23bcd4734d64487a8`를 실행했다. 실제 텍스트 충돌은 §2와 동일한 세 파일이다. 최종 commit, push, PR 생성 및 main 직접 변경은 이 작업에 포함하지 않는다. 따라서 검증 대상은 HEAD/MERGE_HEAD 두 부모 위의 미커밋 작업파일이다.
- 기존 C worktree의 README 수정, 방향 문서와 `Assets/AddressableAssetsData/Windows.meta`는 보존한다. 방향 문서만 통합 worktree의 설계 입력으로 복사했다. 입력 사본과 SHA-256은 `/mnt/d/J2M/evidence/main-conflict-20260912/input/`에 보관한다.
- evidence는 `/mnt/d/J2M/evidence/main-conflict-20260912/`, build는 `/mnt/d/J2M/builds/main-conflict-20260912/` 아래로 분리한다.
- broad 실패의 부모 재현 근거를 위해 동일 main SHA의 detached 진단 worktree `/mnt/d/J2M/worktrees/main-conflict-baseline-20260912`를 `j2m-worktree-add`로 추가했다. 생성 전 D 765 GiB/C 77 GiB 및 생성 전후 audit PASS, 전용 Library를 사용한다. 이 경로의 결과는 부모 baseline evidence이며 통합본 검증을 대신하지 않는다. 다른 통합 계획의 브랜치는 포함하지 않는다.
- 파일 소유권: Driver/binding 담당은 runtime binding/Driver, Editor validator/manifest, EnemyAnimation 테스트와 DrSaturn 및 cache PlayMode 테스트를 소유한다. Coordinator/resolver 담당은 두 runtime 파일과 cache/resolver EditMode 테스트를 소유한다. runner 담당은 shell/tools와 lane 실행을 소유한다. 통합 담당은 문서, 자동 병합 영역 검토와 최종 교차 검토를 소유한다.

### 설계 구체화와 근거

- main에는 binding 재작성 migration 실행 도구가 이미 없다. 폐기 도구를 재도입하지 않고 현행 migration manifest와 직렬화/snapshot round-trip, manifest 검증으로 새 metadata 보존을 확인한다. §4.2의 metadata 보존 계약은 유지하며 검증 경로만 현재 코드에 맞춘다.
- 실패 snapshot은 생성 시 Enemy/Player의 one-shot flags를 제거한 값 상태만 보관한다. Utility 시간은 기존 track에만 있다. 실패 이후 Coordinator를 통과하는 새 의미 입력은 snapshot을 갱신하여 오래된 복구 값보다 우선하게 한다. 첫 후보 준비 실패 역시 catch에서 incoming 값을 보관한다. 정상 성공 후보는 기존 적용 상태를 먼저 옮겨 `Apply`의 Charge/Glide phase-edge 감지를 보존한다. 정상 cache hit은 snapshot 조회나 큰 nullable state override 인자를 추가하지 않는다. Coordinator 밖의 임의 driver 직접 호출을 추적하는 전역 입력 장치는 추가하지 않는다.
- pending 대체는 State 명령이 적용/예약될 때와 지원되는 새 Trigger 명령이 요청될 때 수행한다. unavailable Trigger 자체는 새로 예약하지 않는다. 다만 동일한 의미 입력이 terminal Death를 확정해 교체 pending을 Death로 승격하고 실제로 `Applied` 또는 `Queued`된 뒤에는, Animator가 unavailable인 동안과 재개되어 Death 복원을 소비한 해당 `Apply` 실행이 끝날 때까지 그 입력에 함께 남은 Hit·Jump·Glide 등 하위 Trigger/State cue가 Death를 대체하거나 ordinary State를 예약하지 못한다. Death replacement metadata가 없어 `Unsupported`인 View는 terminal 소유권을 얻지 않으며 하위 cue의 기존 dispatch 규칙을 유지한다. 이는 생존 상태의 일반 last-write-wins를 바꾸지 않는 terminal 입력 전용 예외다. one-shot count는 유지하고 복원 시 신호를 재발행하지 않는다. 초기의 “Trigger 성공 시에만 취소” 안은 비활성 상태의 새 입력 뒤 오래된 Utility 복원이 재생될 수 있어 교차 검토에서 변경했으며, 이후 fatal Hit+Death 조합 검토에서 동일 Death 요청만 보존하던 범위가 State cue와 동시 입력을 포괄하지 못함을 확인했다. 추가 교차 검토에서 pause 해제 직후 Death pending이 즉시 소비된 경우에도 같은 `Apply`의 하위 cue가 복원 상태를 덮을 수 있음과 `Unsupported` Death까지 과도하게 하위 cue를 차단하는 반대 경계를 확인해 terminal 소유 범위를 성공한 복원의 `Apply` 수명으로 제한했다. unavailable Trigger의 일반 취소, fatal Trigger/State에서 Death 예약 보존, pause 해제 직후 supported/unsupported Death와 suppressed Death·동시 State cue, 반복 resync 뒤 ordinary pending 비재생을 함께 검증한다.
- Reset의 수명 정리는 normalization 바깥 finally에서 보장한다. 모든 pulse의 batch normalization 성공은 이 보장과 구분한다. normalization 예외 회귀는 cleanup 경계의 internal 테스트 hook으로 주입하며 공개 API나 매 프레임 경로를 확장하지 않는다.
- 비활성 교체 pending이 아직 소비되지 못하면 일반 resync로 내려가지 않는다. State/Trigger Jump의 반복 inactive resync가 교체 진행률을 일반 pending의 0으로 덮는 것을 막으며 두 dispatch mode 회귀를 추가했다.
- Jump 교체 pending 소비 시 이전 topology snapshot을 지우고 보존 normalized time을 갱신한다. 숨김 동안 만들어진 초기 topology snapshot이 복원 직후 진행률을 덮는 결합 오류를 방지한다. 일반 Jump resync와 topology suspend의 기존 진행률 규칙은 유지하고 hidden→resume 회귀를 추가했다.
- 실패 Player snapshot의 최신 Death 입력은 기존 death override/hold 정책을 적용하고, cancel은 일반 action hold를 취소한다. terminal Death는 기존 정책대로 respawn 경계에서 해제하며 ordinary cancel로 되살리지 않는다. 성공 경로의 hold 정책은 바꾸지 않는다.
- replacement state가 Unsupported여도 현재 Utility track의 timing 적용은 별도로 유지한다. 명시되지 않은 Trigger state로 복원하지 않는 계약과 timing 보존은 독립적이다.
- broad 검증에서 `GameplayEnemyPresentationOrchestrationTests`의 Animator fixture가 main의 명시 binding 없이 예전 기본 상태명을 기대하는 의미 불일치를 발견했다. 원래 BlackEye controller는 `Take 001`만 있어 검증 목적의 `Move`도 정의하지 않았다. 기존 assertion과 case는 유지하고, 해당 fixture가 검증할 State/Trigger와 reference timing을 갖춘 in-memory controller 및 Cue binding을 명시한다. 테스트 소유 객체는 두 사용 지점 모두 finally에서 정리한다. runtime fallback, 실제 controller/Prefab, legacy VFX 요청 경로는 바꾸지 않는다. 이 fixture를 최종 filtered full에 추가하고 broad도 재실행한다.

### 자동 병합 영역 검토

- `GameplayEntityPresentationApplier`는 View reference identity 비교/hash 및 signature 전달을 유지하고, main의 legacy death-retention 읽기/인자를 제거하며 `CompletedPresentationMotions.RecordCompleted`를 사용한다. 양쪽 변경은 자동 병합 결과에 공존한다.
- 양쪽에서 수정된 테스트는 synthetic view factory 계약, legacy death-retention 제거, main의 실제 outro 콘텐츠 및 반복 Flip 시나리오를 함께 검토한다. 폐기 API 검색과 실제 컴파일/테스트 결과는 아래 실행 결과에서 별도로 판단한다.

### 실행 결과 / 미실행 / 잔여 항목

검증 경로의 공통 root는 `/mnt/d/J2M/evidence/main-conflict-20260912`다. 각 실행의 `execution.json`에 명령, 부모 SHA, resolved project path, 환경변수, 소스 hash, XML 경로, fixture 및 case별 결과를 기록했다. 검증은 모두 해당 worktree의 `./run_tests.sh`로 실행했다. 아래 최종 통합 네 lane은 동일한 C#/Prefab/runner hash에서 실행됐고 실행 전후 값도 같았다. 미커밋 최종 tree와 변경 파일 식별값은 `final-source-review.json`, 전체 lane 목록은 `lane-index.json`에서 확인한다.

| 최종 실행 | 결과 | XML / 증거 (공통 root 기준) |
|---|---|---|
| `filtered-full-05` | exit 0. EditMode 363/363, PlayMode 12/12 PASS | `filtered-full-05/results/wsl-unity-full-{editmode,playmode}.xml` |
| `core-02` | exit 0. EditMode 273/273 PASS, PlayMode 111 total = 107 passed + 4 skipped, 실패 0 | `core-02/results/wsl-unity-core-{editmode,playmode}.xml` |
| `ui-02` | exit 0. EditMode 1356/1356 PASS | `ui-02/results/wsl-unity-ui-editmode.xml` |
| `broad-full-03` | exit 1. EditMode 8866 total = 8818 passed + 31 failed + 17 skipped. PlayMode는 EditMode 실패로 미실행 | `broad-full-03/results/wsl-unity-full-editmode.xml` |
| 부모 `main-baseline-filtered-01` | exit 1. 실패 fixture 17개 선택: 469 total = 416 passed + 39 failed + 14 skipped | `main-baseline-filtered-01/execution.json` |
| 부모 `main-baseline-ui-01` | exit 0. EditMode 1356/1356 PASS | `main-baseline-ui-01/results/wsl-unity-ui-editmode.xml` |
| 부모 `main-baseline-broad-01` | exit 1. EditMode 8705 total = 8657 passed + 33 failed + 15 skipped. PlayMode는 미실행 | `main-baseline-broad-01/results/wsl-unity-full-editmode.xml` |

- 최종 filtered는 §8 필터에 `TestRunnerCliBootstrapTimeoutTests`와 `GameplayEnemyPresentationOrchestrationTests`를 추가했다. EditMode 17개 / PlayMode 2개 fixture의 양수 실행 수를 XML로 확인했다. timeout 새 fixture 16개, 보완한 Enemy orchestration 11개, binding Editor validation 27개가 모두 통과했다. `J2M_DRIVER_CACHE_PERFORMANCE=1`의 WSL 전달을 확인해 opt-in cohort 2개도 실제 실행했다. 이 결과는 성능 개선 증명이 아니다.
- 실제 DrSaturn Prefab 9개 PlayMode가 Death, Airborne보다 Death 우선, Utility Windup/Recovery의 진행률과 남은 시간, 비활성 복원/만료/Death 전환을 검증했다. JPeter lifecycle 2개와 Pause activation 1개도 통과했다. 실제 Prefab 인스턴스의 Animator 상태/시간/one-shot count 자동 검증이며 screenshot/manual 육안 검증으로 확대하지 않는다. Editor authoring/controller/manifest/asset 검증은 filtered에 포함했다.
- core의 skipped 4개는 기존 Terminal/entry RenderTexture pixel 및 네 aspect ratio capture case다. `-nographics` 기본 lane에서는 실행되지 않으며 `UNITY_GRAPHICS=1` 또는 전용 capture lane이 필요하다. 이번 DrSaturn 상태 복원에는 자동 Animator/Editor 검증을 사용했다. 별도 육안·스크린샷·Player build 표시 검증은 실행하지 않았고 완료로 간주하지 않는다.
- runner syntax, default/900 print-config, core/ui/KBO/S3 및 full900 dry-run, worktree-path regression, timeout 경계/잘못된 입력 회귀, visual interruption 검사는 PASS. Python lifecycle 28개 PASS. 소스 identity를 검사하는 Python evidence 검사는 문서와 index를 고정한 뒤 실행하며, **그 최종 판정과 원본 로그 경로는 외부 `runner/final-runner-checks.json`에 기록**한다. 문서에 결과를 쓰기 위해 검사 입력을 다시 변경하는 순환을 피한다.

초기 checkpoint도 보존했다. `filtered-full-01`은 cold refresh 약 249.444초 뒤 외부 timeout exit 124였고, 생성된 EditMode XML은 322 total / 318 passed / 2 failed / 2 skipped였다. Utility timing(0.5 기대/0 실제)과 frozen-negative Player cleanup(Idle 기대/Push 실제)은 이번 통합 회귀로 수정했다. `filtered-full-02` 331+12, `filtered-full-03` 336+12, timeout 보완 뒤 `filtered-full-04` 352+12가 각각 통과했으나 최종 판정은 위 마지막 checkpoint를 사용한다. 편집 중 실행했던 live-source-identity evidence 검사는 입력 변화로 중단했으며 성공으로 집계하지 않는다. `broad-full-01`은 timeout/no XML, `broad-full-02`는 8866 total / 8817 passed / 32 failed / 17 skipped였다.

### Baseline 분류와 완료 범위

최종 broad의 **31개 실패 모두** 부모 main broad에서 **동일 case와 동일 failure message**로 재현됐다. `baseline-comparison-final.json`의 integration-only failure와 different-message 목록은 모두 비어 있다. UI 12개는 양쪽 isolated ui에서 통과하지만 broad에서 같은 원문으로 실패한다. 따라서 isolated ui 통과를 broad 통과로 바꾸어 해석하지 않는다.

부모 filtered에서 UI는 더 앞선 Localization 초기화 오류로 실패해 처음에는 원인이 미확정이었다. 이를 숨기지 않고 부모 ui와 broad를 추가 실행해 같은 broad 문맥에서 확인했다. 부모 broad에는 테스트/runtime 변경 없이 watchdog 285→900 및 외부 test timeout 300→915 두 상수만 진단용으로 적용했다. 원본과 exact diff는 `input/main-baseline-timeout-*`, 실행 중 동일성 및 완료 후 main 원본 바이트 복원/clean 상태는 `main-baseline-broad-01/timeout-restore.json`에 보관했다. 별도 Unity 직접 호출이나 종료 보호 우회는 하지 않았다.

남은 baseline 31개는 Material 2, Flip 1, Audio 2, Player parity 1, VFX migration 4, View projection 1, legacy residue 1, stage installer 1, profile readiness 2, Steam exclude documentation 2, broad UI 12, replay 1, attack 1이다. 원인과 부모 blob/section 비교는 `broad02-driver-triage.md`, `broad02-additional-triage.md`, `coordinator-resolver-review.md` 및 대응 JSON에 있다. 이들 기존 assertion을 약화하거나 legacy route를 되살리지 않았다. 이번 직접 관련 fixture의 binding 누락만 보완하여 broad 실패를 32→31로 줄였다.

- 충돌 해결과 코드 구현: 완료. 일반 resync와 교체 복원, 실패 복구 및 소유권 계약을 구분해 구현했다. 파일 전체 ours/theirs 선택은 사용하지 않았다.
- 컴파일과 touched 검증: 최종 filtered/core/ui 통과 및 실제 Prefab Animator/Editor 검증 완료. 발견했던 통합 회귀 두 건과 binding fixture 의미 불일치는 수정 후 재검증했다. 현재 확인된 미해결 touched 회귀는 없다.
- broad: 실행했으나 baseline 31개 실패로 red. broad PlayMode는 runner의 EditMode 실패 중단 규칙 때문에 미실행이며 core/filtered PlayMode로 대체 완료 표시하지 않는다.
- 잔여 한계: Reset은 예외 시 수명 상태를 정리하지만 첫 normalization 실패 뒤 모든 남은 pulse normalization의 성공까지 보장하지 않는다. 후보 준비 실패 시 candidate Animator의 부분 변경 자체는 rollback하지 않는다. 실패 snapshot의 최신 입력 우선순위는 Coordinator를 통과한 입력에 적용한다. 다른 Prefab의 동등성, screenshot/manual 표시, 전체 성능 개선, 전체 PR 리뷰는 검증하지 않았다.
- 검토용 미커밋 merge와 evidence를 준비했다. broad baseline 및 미실행 범위를 명시한 상태이며 전체 green/무조건 merge-ready를 주장하지 않는다. 최종 commit, push, PR 생성과 main 직접 변경은 수행하지 않았다. 기존 C worktree 사용자 파일은 변경하지 않았다.

### Broad 검증 timeout 보완

`broad-full-01`은 기본 외부 300초에 도달해 exit 124, XML 없음으로 종료했다. KBO는 `NO_MUTATION`, source hash는 동일했다. 이는 baseline 실패 수를 확보한 실행이 아니라 검증 미완료다.

§6의 원래 runner 변경 범위는 usage 병합이었으나, 필터 없는 현재 suite를 완주하기 위해 명시적 `UNITY_TEST_TIMEOUT_SECONDS` 설정을 추가한다. 기본 bootstrap watchdog 285초와 외부 timeout 300초는 유지하며 설정값은 watchdog 예산, 외부 timeout은 설정값+15초다. bootstrap에는 명시적 인자로 전달하고 SessionState의 duration/absolute deadline을 보존한다. 잘못된 값은 실행 전에 거부한다. 강제 종료 grace, KBO source/integrity, S3 capture/evidence 및 visual interruption 보호를 제거하거나 우회하지 않는다. 기본/명시값/잘못된 값 runner 회귀와 syntax/dry-run/lifecycle/interruption 검사, 새 runner/Bootstrap 상태의 filtered/core/ui/broad 재실행으로 대체 검증한다.
