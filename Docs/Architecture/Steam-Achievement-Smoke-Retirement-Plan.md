# Steam 업적 검증 구조 정리 계획

> **현재 구현 상태:** smoke·계측 제거와 제품 테스트 이관을 구현했다. 현재 결과는 9절을 따른다. 1~8절은 삭제 전 검토·계획 당시의 기록으로 보존하며, 그 안의 미구현 표현은 당시 상태를 뜻한다.

## 1. 상태와 목적

- 검토일: 2026-09-11. 기준 코드: `4aea5fc66174885583d154c864f02f0474bc15c8` 및 현재 작업 디렉터리.
- 사용자 제공 성공 확인을 계기로, Steam 연동 확인용 smoke와 불필요한 계측을 제거할 범위를 정한다.
- **이 문서는 재검토와 제거 계획이다. 아래 코드 삭제·축소는 아직 실행하지 않았다.** 실제 Steam 계정, 설치 빌드, 초기화·재획득·재실행을 이번 검토에서 재검증하지 않았다.
- 현재 코드에는 정상 제품 업적 발행과 Spacewar smoke가 있다. 전시 참가자 초기화·자동 재실행 및 상세 live callback collector는 계획 문서만 확인된다. 사용자 성공 확인을 현재 체크아웃의 reset 구현 완료 증거로 바꾸어 기록하지 않는다.

정리 후에도 정상 클리어 → 캠페인 저장 → 제품 업적 장부 → Steam 발행 → 다음 실행에서 확인하는 경로를 유지한다. 성공 확인용 실행 모드, 측정 상태, 출력만 걷어낸다.

## 2. 제거·축소할 기존 코드

아래 표의 제거는 후속 구현의 목표 상태다. 삭제하는 Unity 소스는 해당 `.meta`와 함께 처리한다.

| 처리 | 대상 | 제거 후 상태와 동반 작업 |
| --- | --- | --- |
| 삭제 | `Packages/com.j2m.platform.steam/Runtime/SteamAchievementSmokeCoordinator.cs` | Spacewar Set/Store·콜백 대기·검증·결과 출력 상태 머신 제거. 정상 제품 Publisher가 업적 콜백을 소유 |
| 삭제 | 같은 Runtime의 `SteamAchievementSmokeDiagnostics.cs`, `SteamAchievementSmokePhase.cs`, `SteamAchievementSmokeOutcome.cs`, `SteamAchievementSmokeFailureKind.cs`, `SpacewarAchievementSmokePolicy.cs` | smoke 전용 DTO·enum·AppID 480/시험 업적 정책 제거. 제품 mapping은 유지 |
| 축소 | [SteamPlatformRegistration](../../Packages/com.j2m.platform.steam/Runtime/SteamPlatformRegistration.cs), [SteamPlatformRuntimeFactory](../../Packages/com.j2m.platform.steam/Runtime/SteamPlatformRuntimeFactory.cs) | `-j2mSteamSmoke`, `-j2mSteamAchievementSmoke` 파싱, 전용 상수·bool 전달·전용 overload 정리. `-j2mPlatformProvider steam` 선택은 유지. 폐기 플래그는 더 이상 smoke를 실행하거나 제품 발행을 차단하지 않음 |
| 축소 | [SteamPlatformRuntime](../../Packages/com.j2m.platform.steam/Runtime/SteamPlatformRuntime.cs) | smoke 생성·BeginSession·Tick·Shutdown·Diagnostics 접근과 smoke logger, JSON 출력, 결과 일회 출력 상태 제거. 정상 publication feature의 수명 호출은 유지 |
| 축소 | [SteamProductAchievementPublicationFeature](../../Packages/com.j2m.platform.steam/Runtime/ProductAchievements/SteamProductAchievementPublicationFeature.cs) | `_achievementSmokeRequested`와 해당 시작 차단 분기·생성자 인자 제거. 의존성 없음, 초기화 실패, 중복 시작, 종료 후 시작 방지는 유지 |
| 삭제 | Runtime의 overlay 관측 경로 | `ObserveDelayedOverlayEnabledForSmoke`, `ObserveOverlayEnabled`, overlay callback 등록·해제·observer, 관측 가능 여부·활성 상태·활성/비활성 횟수·최대 300회 관측 상태 제거 |
| 축소 | [ISteamNativeApi](../../Packages/com.j2m.platform.steam/Runtime/ISteamNativeApi.cs), [SteamworksNetNativeApi](../../Packages/com.j2m.platform.steam.steamworksnet/Runtime/SteamworksNetNativeApi.cs) | 위 overlay 관측의 조회/등록/해제 seam과 adapter handle·observer 제거. fake 구현도 맞춤. 현재 검색된 소비자는 진단과 테스트이며 게임 일시정지 등 제품 동작에 연결되지 않음 |
| 삭제 | `SteamDllCheckObservation.cs` 및 `ObserveDllCheck` 연결 | adapter의 `DllCheck.Test()` 관측 호출, interface 메서드, Runtime 저장값, 진단 DTO 필드와 전용 fake/test 제거. `Packsize.Test()`, 실제 native 초기화·로드 오류 처리는 유지 |
| 축소 | [SteamPlatformDiagnostics](../../Packages/com.j2m.platform.steam/Runtime/SteamPlatformDiagnostics.cs) | overlay/DLL 관측 필드와 `CallbackPumpCount`, `CallbackAttemptCount`, `ShutdownCallCount` 제거. 기존 상태·초기화 결과·AppID·계정 유효성·로그인 여부·실패 이유·예외 종류는 유지 |
| 축소 | Runtime의 횟수·출력 전용 상태 | 실제 pump/shutdown 호출은 유지하고 측정용 증가 연산만 제거. `initializationFailureReason`/`CaptureInitializationFailure`처럼 smoke 출력만 위한 상태도 제거. 초기화 결과와 현재 실패 결과는 기존 결과 객체에 유지 |
| 축소 | [SteamAchievementPublisher](../../Packages/com.j2m.platform.steam/Runtime/ProductAchievements/SteamAchievementPublisher.cs) | `_statsStoredObservationCount`, `_lastStatsStoredResult`, `StatsStoredObservationCount`, `LastStatsStoredResult` 제거. `ObserveStatsStored`의 기록·lock은 없애고 부작용 없는 수신으로 단순화 |

Overlay 관측 제거는 Steam Overlay 자체를 끄는 작업이 아니다. Overlay를 켜고 끄는 제품 설정이나 native payload는 변경하지 않는다.

DLL 관측은 현재 adapter가 `UpstreamDisabled`로 보고하는 진단이다. 단, 현재 호출은 초기화의 바깥 try 안에 있어 예외가 초기화를 중단할 수 있다. 호출 제거로 이 진단 전용 실패 경로도 사라진다. 따라서 모든 예외 경로까지 완전히 동일하다고 주장하지 않고, 실제 packsize/native 초기화 오류 분류가 유지되는지 검증한다.

## 3. 반드시 남길 코드와 동작

| 유지 대상 | 남기는 이유 |
| --- | --- |
| Platform provider 선택·등록·availability와 application host | Local 기본값, Steam 명시 선택, 미등록/불가 provider 오류, host 단일 소유권은 정상 부팅 계약 |
| Steam runtime의 native Initialize·RunCallbacks·Shutdown 및 멱등성 | 한 소유자가 초기화·pump·종료를 수행. 횟수 기록을 없애도 동작 자체와 종료 순서는 유지 |
| `GetAppId`, `IsSteamIdValid`, `IsLoggedOn` | 정상 Publisher 시작과 배치 진입의 준비 상태 판정에 사용 |
| 제품 `SteamAchievementMapping`과 런타임 schema 조회 | 5개 제품 업적의 정확한 ID/API Name 및 존재 여부 판정 |
| Publisher의 pre-read, SetAchievement, StoreStats, FIFO와 작업 상태 | 이미 획득한 업적 처리, 배치당 Store 한 번, 중복/재진입/동시 배치 처리 |
| 이름 있는 full-unlock 콜백 판정 | AppID·API Name·완전 달성·활성 작업·Store 시작 여부를 확인해야 해당 항목이 Submitted가 됨 |
| Publisher의 monotonic clock, timeout, quarantine, 실패 격리 | 늦은 콜백을 다음 요청 성공으로 잘못 해석하지 않도록 하는 기능 계약. 성능 계측이 아님 |
| `ISteamAchievementApi`의 콜백 pair 및 두 Observation DTO | 원자적 등록, 단일 소유권, 등록 실패 시 기존 소유권 보존, 해제 계약을 이번에 유지 |
| adapter의 `UserStatsStored_t` / `UserAchievementStored_t` 변환·등록·해제 | 기존 콜백 pair 계약 유지. 통계 콜백은 정상 제품 발행의 성공·실패·quarantine을 결정하지 않음 |
| ProductAchievementCoordinator의 earned/pending, in-flight, 시작 시 재조정 | Submitted 뒤에도 pending을 보존하고 새 application lifetime의 AlreadySatisfied와 저장 성공 후 해소 |
| 제품 장부 저장소와 원자 저장·백업/복구 | 업적 내구성과 기존 저장 계약. 로그 수집 구조가 아님 |
| 캠페인 정상 기록 통합과 startup reconciler | 캠페인 저장 이후 제품 지급, 재실행 시 미지급 복구 |
| 제품 composition·session handoff·subsystem reset | 제품과 Steam의 시작 순서 차이, Unity 재초기화, 중복 세션 방지 |
| Steamworks 의존성 inventory, native payload, release export와 SteamPipe 도구 | 실제 빌드·배포·의존성 계약. smoke 성공만으로 불필요해지지 않음 |

`ObserveOptionalRuntimeDiagnostics`라는 이름 아래 identity/login 조회가 섞여 있다. overlay 부분을 제거한 뒤 세션 관측을 나타내는 이름으로 정리할 수 있지만, 메서드 전체를 삭제하면 정상 Publisher 시작 조건이 훼손된다.

이번 계획은 `UserStatsStored`의 수신·DTO까지 제거하지 않는다. pair 계약 변경은 별도 설계 대상이며 이번 정리의 필수 후속 작업도 아니다. 통계 콜백을 남기는 이유는 기존 transport 계약 보존이지, 현재 제품 성공 판정에 필요하기 때문이 아니다.

## 4. 초기화·재실행 및 미구현 계측의 처리

| 구조 | 이번 결정 |
| --- | --- |
| [Exhibition Participant Reset 계획](./Exhibition-Participant-Reset-Implementation-Plan.md) | 보존. 업적 초기화·한 번 재실행이라는 기능 의도는 이번 삭제 대상에 포함하지 않음. 현재 구현은 확인되지 않음 |
| 해당 계획의 Pending/Ready, 명시적 시작 보류, reset/Publisher 콜백 소유권 전환, 대상·계정 확인, 저장소 초기화, 재실행 도우미 | 전시 기능 구현 시 필요한 계획으로 남김. smoke 플래그를 대체 시작 보류 수단으로 재사용하지 않음 |
| [Live Callback Test 계획](../Testing/Steam-Achievement-Live-Callback-Test-Plan.md)의 event/sink, collector, background writer, JSONL/summary, 분석기, 성능 비교, 진단 인자 | 미구현이므로 삭제할 코드가 없음. 사용자 성공 확인 후 본 정리 작업에서는 구현하지 않음. 기존 계획 문서는 검토 이력으로 보존하며 새 필수 구현 요구사항으로 승격하지 않음 |
| 별도 Verify 프로세스, 자동 복구·반복 재시도·단계별 보정 상태 | 전시 단순화 계획에서 이미 제외한 구조. 이번 정리 과정에서도 추가하지 않음 |

현재 일반 adapter/API는 `ClearAchievement`·`ResetAllStats`를 노출하지 않는다. 초기화용 새 기능 경계를 이번 smoke 정리에 끼워 넣지 않는다. 기존의 단일 application lifetime 제한을 없애거나 `OnSteamInitialized`를 다시 호출하는 방식으로 재시작 기능을 구현하지 않는다.

## 5. 테스트에서 제거할 것과 남길 것

아래 Steam 테스트 경로는 `Packages/com.j2m.platform.steam/Tests/` 기준이다. 테스트 fake의 호출 횟수는 실제 동작 검증 수단이므로 runtime 계측 제거와 함께 없애지 않는다.

| 대상 | 처리 |
| --- | --- |
| `EditMode/SteamAchievementSmokeCoordinatorTests.cs` | smoke 전용 fixture와 `.meta` 삭제 |
| `EditMode/SteamAchievementRuntimeIntegrationTests.cs` | smoke 기반 fixture 삭제. 단일 pump·해제 후 native 종료·해제 예외가 native 종료를 막지 않는 의도는 제품 integration/PlayMode에서 보존 |
| `SteamProductAchievementPublicationIntegrationTests.AchievementSmoke_OwnsOnlyCallbackPairAndLeavesProductPending` 및 `BaseSteamSmokeWithoutAchievementSmoke_AllowsProductPublisherOwnership` | 폐기되는 두 실행 모드의 테스트 삭제 |
| `PlayMode/SteamPlatformRuntimePlayModeTests.AchievementSmoke_ReusesHostCallbackPumpAndSingleAdapterLifecycle` | 정상 제품 경로로 교체. 기존 host와 한 adapter가 pump/lifecycle을 소유함을 검증 |
| `SteamPlatformRuntimeTests`, `SteamPlatformRegistrationTests`, PlayMode 내 smoke 로그·플래그·overlay·DLL 관측 assertion | 해당 계약만 삭제/수정. 초기화 실패, callback fault, 중복 종료 방지는 유지하고 fake 호출로 검증 |
| `EditMode/FakeSteamAchievementApi.cs` | schema와 callback 기본 인자의 `SpacewarAchievementSmokePolicy` 의존 제거. 테스트 전용 상수 또는 명시 인자로 치환하고 제품 테스트의 AppID/이름은 보존 |
| `SteamAchievementPublisherTests`의 count/last-result assertion | 삭제. StatsStored OK/오류만으로 배치 완료가 바뀌지 않는 결과 assertion은 유지 |
| `SteamProductAchievementPublicationIntegrationTests.SubmittedPending_IsRemovedOnlyAfterFreshApplicationLifetimePreRead` | 유지: 새 실행 확인 이후 pending 해소 |
| 같은 fixture의 `SecondSteamRuntimeSession_IsRejectedWithinSameApplicationLifetime`, `CallbackRegistrationFailure_DoesNotDisposeUnownedCallbacksAndLeavesProductUnavailable`, `CallbackPumpFault_StopsPublicationClearsInFlightAndKeepsPending` | 유지: 세션·소유권·실패 계약 |
| 같은 fixture의 `EitherShutdownOrder_DisposesCallbacksAndNativeExactlyOnce` | 유지하되 overlay 해제 기대만 제거. 제품 콜백 해제 → native 종료와 정확히 한 번 호출 유지 |
| `SteamAchievementPublisherTests.StatsStoredError_IsDiagnosticAndDisposeExceptionsRemainContained` | 계측 제거에 맞게 이름/관측 assertion 수정. 통계 콜백 비간섭·dispose 예외 격리 의도 유지 |
| `SteamPlatformArchitectureTests`, `SteamworksNetDependencyInventoryTests` | smoke 기반 이름·허용 표면 검사와 overlay/DLL 기대 수정. 제품 API·의존 방향·reset 금지 경계는 유지 |
| mapping·adapter·제품 저장·campaign integration/startup 테스트 | 유지. 테스트 전체를 임시 진단으로 분류하지 않음 |

`SteamAchievementPublisherTests`에서는 다음 동작을 명시적으로 보존한다.

| 메서드 | 유지할 검증 |
| --- | --- |
| `NamedCallback_SubmitsExactlyOnceRegardlessOfStatsOrder` | 통계 콜백 순서에 관계없이 이름 있는 콜백으로 한 번 완료 |
| `Batch_SetsAllCandidatesStoresOnceAndWaitsForEveryExactNamedCallback` | 배치당 Store 한 번, 모든 대상의 정확한 이름 콜백 대기 |
| `StatsStoredOkAlone_DoesNotCompleteAndEventuallyTimesOut` | 통계 OK만으로 성공하지 않고 미확인 항목은 timeout |
| `DelayedPriorStatsError_IsDiagnosticAndDoesNotContaminateNamedOperations` | 이전 통계 실패가 현재·대기 작업에 영향을 주지 않음. 계측 assertion과 이름의 `IsDiagnosticAnd`는 정리 |
| `ForeignWrongAndPartialCallbacks_DoNotCompleteTarget` | 다른 AppID·이름·부분 진행 콜백 배제 |
| `StatsStoredError_DoesNotCompleteOrStopQueuedNamedOperations` | 통계 실패가 대기 중 이름별 작업을 완료·중단하지 않음 |
| `DisposeInFlight_CompletesUniformUnavailableAndIgnoresCapturedLateCallbacks` | 종료 후 늦은 콜백은 무시하고 진행 중 항목은 Unavailable로 완료 |

`SteamAchievementRuntimeIntegrationTests.AchievementCallbackDisposalException_DoesNotBlockNativeShutdown`의 의도는 native 종료까지 연결되는 제품 integration 테스트로 이관한다. Publisher 단위 예외 테스트만으로 이를 대체하지 않는다. `SteamPlatformRuntimeTests.SmokeInitializationFailure_PreservesOriginalKindWhenCleanupShutdownThrows`도 출력 검증만 없애고 초기화 오류와 cleanup 오류가 함께 발생했을 때의 결과·native 종료 한 번을 보존한다.

삭제 심벌을 문자열로 금지하는 `SpacewarSmokeTokens_DoNotLeakIntoProductProductionSources`, `ProductPublisher_OwnsNoSpacewarPolicyOrSecondCallbackPump`, `ProductAchievementArchitectureTests.ProductProductionModule_HasNoStoreTransportOrTechnicalAcceptanceTokens`는 재유입 방지 검사로 유지할 수 있다. 금지 문자열과 역사 문서의 언급은 실행 코드의 제거 잔재와 구분한다. `FakeSteamNativeApi`의 숫자 `480`처럼 삭제 타입에 의존하지 않는 테스트 값은 일괄 제거하지 않는다.

## 6. 코드 정리와 함께 갱신할 문서

| 문서 | 구현 시 변경 |
| --- | --- |
| [Steam Provider Lifecycle](../../Packages/com.j2m.platform.steam/Documentation~/Steam-Provider-Lifecycle.md) | smoke 실행 인자·Spacewar 소유권·overlay/DLL 관측·종료 설명 삭제. 정상 제품 콜백 소유권과 단순해진 종료 순서 설명. StatsStored를 diagnostics로 보존한다는 설명도 pair 수신 유지·제품 판정 비사용으로 변경 |
| [Product Achievement Foundation](./Product-Achievement-Foundation.md) | smoke에 의한 Publisher 차단과 overlay 해제 설명 제거. StatsStored의 진단 기록 표현을 “pair 수신은 유지하며 제품 결과에 사용하지 않음”으로 변경 |
| [SteamworksNet Adapter](../../Packages/com.j2m.platform.steam.steamworksnet/Documentation~/SteamworksNet-Adapter.md) | callback pair와 단일 pump 설명 유지. interface 변경과 모순되는 설명이 있는지 확인 |
| [Platform Provider Selection Validation](../Testing/Platform-Provider-Selection-Validation.md) | provider 선택 검증은 유지. 삭제 심벌/진단 assertion 참조가 있으면 해당 부분만 갱신 |
| 기존 전시·live callback 계획 | 원문은 이번 문서 작성에서 수정하지 않음. smoke 제거 후 전시 계획의 smoke 병행 금지 문장은 과거 경계임을 구현 변경 시 정리 |

Steamworks 실제 게시 상태나 사용자 성공 범위를 이번 코드 검색으로 추정해 기존 release 상태 값을 바꾸지 않는다. 새 정리 문서는 Architecture README에서 연결한다.

## 7. 구현 순서와 검증 기준

1. smoke 전용 코드·플래그·제품 차단 분기와 전용 테스트를 함께 제거한다. fake의 Spacewar 참조도 정리한다.
2. overlay/DLL 관측과 runtime 횟수·JSON 출력을 제거하고 diagnostics를 축소한다. identity/login·실패 결과를 보존한다.
3. Publisher의 StatsStored 기록만 제거한다. callback pair·DTO·adapter 소유권은 유지한다.
4. 테스트의 실제 동작 검증을 보존하고 문서의 현재 상태 설명을 코드와 맞춘다. 삭제 파일 `.meta`, 참조·심벌 잔재와 실제 변경 diff를 확인한다.

후속 코드 변경은 해당 worktree에서 `./run_tests.sh core`를 실행한다. `core --filter`는 core 범위를 넓혀주지 않으므로 Steam/제품 fixture 전체가 필요하면 `./run_tests.sh full --filter <fixture>`를 사용하고 XML의 실제 실행 수를 확인한다. filtered full 결과를 unfiltered full 통과로 보고하지 않는다.

최소 targeted 대상은 `SteamAchievementPublisherTests`, `SteamProductAchievementPublicationIntegrationTests`, `SteamPlatformRuntimeTests`, `SteamPlatformRegistrationTests`, `SteamPlatformArchitectureTests`, `SteamworksNetAchievementAdapterTests`, `SteamworksNetDependencyInventoryTests`, 제품 장부·세션·startup 회귀 및 `SteamPlatformRuntimePlayModeTests`다. 새 진단 프레임워크나 전용 분석기를 검증 목적으로 추가하지 않는다.

runner가 지원하는 세미콜론 구분 filter를 사용한 실행 예시는 다음과 같다. 두 명령은 계획이며 이번 문서화에서 실행하지 않았다. filtered full도 전체 solution build를 먼저 실행하므로 관련 없는 build 실패와 대상 테스트 결과를 구분한다.

```bash
./run_tests.sh core
./run_tests.sh full --filter 'Game.Platform;Game.Product.Achievements;CampaignStageAchievement;CampaignStageFlow'
```

새 테스트 결과와 로그는 각각 `TEST_RESULTS_ROOT`, `TEST_LOG_ROOT`를 `/mnt/d/J2M/evidence` 아래 실행별 디렉터리로 지정한다. 새 Player 출력은 `/mnt/d/J2M/builds`에 둔다. 새 worktree가 필요하면 저장 정책에 따라 `j2m-worktree-add`와 D 여유 30 GiB 조건을 적용한다. Unity는 `run_tests.sh`로 현재 worktree를 검증한다.

실제 Windows Player에서는 일반 Steam 실행 → 정상 업적 획득 → 종료 → 같은 계정 재실행 후 상태 유지·pending 해소를 확인한다. overlay 관측 제거 후 실제 Overlay 표시도 수동 확인한다. 이는 초기화·자동 재실행 기능의 검증을 대신하지 않는다. UI/Scene/Prefab을 변경하지 않는 정리라면 UI lane은 미실행 이유를 기록하고, 범위가 확장되면 해당 검증을 추가한다.

## 8. 이번 재검토 결과와 남은 작업

- 서브 에이전트 2명이 runtime 제거 반례와 테스트·문서 의존성을 각각 재검토했다. 주 검토자가 실제 source와 runner 계약을 대조했다.
- 1차 검토 대비 구체화: identity/login 유지, callback pair 유지와 StatsStored 기록 삭제 분리, fake의 Spacewar 기본값 치환, DLL 관측 예외 경로 차이, smoke 기반 수명 테스트 의도 보존을 명시했다.
- 이번 변경은 이 계획 문서와 Architecture index 연결이다. 코드·설정·기존 사용자 계획 문서는 변경하지 않는다.
- 이번 검증: source/참조 검토 및 두 서브 에이전트의 문서 최종 검토 완료. 문서 상대 링크 14개와 index 연결 검사 통과, `git diff --check` 및 새 문서의 행 끝 공백 검사 통과.
- 미실행: Unity core/ui/full, Player 빌드, 실제 Steam 초기화·재획득·재실행. 이유: 코드 삭제를 수행하지 않은 문서화 단계다.
- 남은 작업: 2절 코드 삭제·축소, 5절 테스트 정리, 6절 동반 문서 갱신, 7절 실행 검증. 구현 완료·Steam 실연동 재검증·프로젝트 전체 회귀 통과를 주장하지 않는다.

## 9. 구현 및 검증 기록 — 2026-09-11 KST

### 구현 결과

- Runtime smoke 소스 6개와 DLL 관측 DTO 1개, smoke fixture 2개를 각각 `.meta`와 함께 삭제했다. 등록·factory·runtime의 smoke 인자와 JSON 출력, 정상 Publisher 시작 차단 분기도 제거했다.
- `ISteamNativeApi`는 Packsize, Initialize, RunCallbacks, Shutdown, AppID, identity validity, login 조회의 7개 메서드만 유지한다. Overlay 관측과 DLL 진단 호출·fake seam을 제거했지만 Overlay 설정, 실제 native 초기화·오류 처리, vendored Steamworks와 배포 도구는 변경하지 않았다.
- `ObserveSessionIdentity`가 identity/login을 각각 예외 격리하여 조회한다. Diagnostics는 provider/state, 초기화 시도·성공·native 결과, AppID·identity/login, 마지막 실패·예외 타입으로 축소했다. 초기화 원래 결과와 종료 오류는 기존 결과 객체와 diagnostics에 각각 남는다.
- `ObserveDllCheck`에서만 발생하던 초기화 중단 경로는 해당 호출과 함께 사라졌다. Packsize 호출과 실제 native Initialize의 예외 분류는 보존한다.
- Publisher의 StatsStored 횟수·마지막 결과와 기록용 lock을 제거했다. StatsStored 수신은 부작용 없는 메서드로 남기며 원자적 callback pair, 두 Observation DTO, adapter 변환·소유권, 정확한 이름 콜백, monotonic timeout, FIFO와 quarantine은 유지한다.
- fake의 schema는 제품 fixture가 주입하고 callback AppID/API Name은 명시 인자로 받는다. 실제 동작 검증용 fake 호출 횟수는 유지했다. 공통 public `HasExactOptInFlag`는 store-neutral 계약으로 유지한다.
- 기존 미추적 전시 초기화·live callback 계획 원문과 사용자 Addressables 변경을 보존했다. 전시 계획의 smoke 병행 금지와 live 계획의 smoke 소유권 설명은 삭제 전 경계에 대한 이력이다. 초기화·자동 재실행·collector 구현이나 기존 application lifetime 제한 완화는 수행하지 않았다.

### 테스트·문서 이관

- smoke 삭제 전에 제품 integration의 단일 Tick/pump, 해제 예외에도 native 종료 한 번, 제품 PlayMode의 host 하나·프레임당 pump 한 번·pair 수명 검증을 먼저 통과시켰다. PlayMode 테스트 asmdef에 Domain/Composition 참조와 Composition의 friend-test 접근만 추가했다.
- 초기화 실패와 cleanup 종료 예외의 동시 발생, identity/login false·예외 4개 경우, Packsize 호출 예외 4개 경우를 제품 경로에서 검증한다. 기존 fresh-lifetime pending 해소, 두 번째 세션 거절, FIFO·timeout·quarantine, adapter pair·장부 복구·캠페인 테스트는 유지했다.
- `SteamworksNetNativeLoadPlayModeTests`의 삭제된 diagnostics 횟수 assertion 3개를 제거하고 native-load, typed failure, explicit Steam, no fallback, disabled host tick 검증을 유지했다.
- Lifecycle, Product Achievement Foundation, SteamworksNet Adapter 문서와 Architecture index를 갱신했다. Provider Selection Validation은 삭제 심벌 참조가 없어 유지했다. 금지 문자열 guard와 역사 문서는 실행 코드 잔재로 취급하지 않는다.

### 검증 상태

- Evidence root: `/mnt/d/J2M/evidence/steam-smoke-retirement-20260910T203905Z`.
- 삭제 전 이관 검증: `full --filter 'SteamProductAchievementPublicationIntegrationTests;SteamPlatformRuntimeTests;SteamPlatformRuntimePlayModeTests'`, EditMode 40/40, PlayMode 7/7 통과 (`migration-retry`). 최초 시도는 Unity 생성 `.csproj`의 새 assembly 참조 누락으로 build에서 중단되었다. ignored 생성 프로젝트를 asmdef와 동기화한 뒤 통과했다.
- 최종 targeted 최초 실행은 사용자 중단으로 XML이 완성되지 않았다. 해당 실행을 통과 증거로 사용하지 않고 `targeted-resumed`에서 재실행해 통과했다.
- 최종 `./run_tests.sh full --filter 'Game.Platform;Game.Product.Achievements;CampaignStageAchievement;CampaignStageFlow'`: solution build 및 EditMode 439/439, PlayMode 29/29 통과 (`targeted-resumed`). 두 XML의 leaf test-case를 집계해 예상 28 fixture(EditMode 24, PlayMode 4)가 모두 실제 실행되었고 failed/skipped/inconclusive가 0임을 확인했다. 삭제한 두 fixture는 없고 필수 이관 테스트, identity/login 4case, Packsize 4case가 모두 Passed다. fixture별 수치는 `targeted-fixture-summary.txt`에 보존했다.
- 최종 `./run_tests.sh core`: build 및 EditMode 254/254, PlayMode 111 total / 107 passed / 4 skipped / 0 failed 통과 (`core`). skip은 통과 수에 합산하지 않았으며 `core-summary.txt`에 이름과 사유를 기록했다.
- 두 최종 명령은 현재 worktree의 `./run_tests.sh`로 실행했고 각각 `TEST_RESULTS_ROOT=<evidence>/<lane>/results`, `TEST_LOG_ROOT=<evidence>/<lane>/logs`를 지정했다. `tested-source-manifest.txt`는 base HEAD `4aea5fc66174885583d154c864f02f0474bc15c8`와 검증한 변경 소스/메타의 SHA-256 또는 삭제 상태를 기록한다.
- 정적 확인: `git diff --check`, 삭제 소스/메타 9쌍, production 삭제 심벌 잔재 검색, 갱신 문서 상대 링크, 기존 사용자 파일 SHA-256 보존 확인 통과. 저장 정책 audit와 runner current-worktree path/dry-run 확인도 통과했다. 새로운 worktree는 만들지 않았다.
- 허용되는 결과 표현은 core 및 위 targeted 범위의 통과다. filtered full을 unfiltered full 통과나 프로젝트 전체 회귀 완료로 표현하지 않는다.
- 두 서브 에이전트가 runtime 경계와 테스트·문서 diff를 각각 재검토했고 수정이 필요한 문제를 발견하지 못했다.
- 미실행: UI lane (UI/Scene/Prefab 변경 없음), unfiltered full (이번 touched cluster 범위 밖), Windows Player 빌드·실제 Steam 신규 획득·같은 계정 재실행·Overlay 표시 (대화형 Windows/Steam 수동 검증 미수행). 자동 테스트 통과로 이 범위의 성공을 주장하지 않는다. 후속 수동 Player 출력은 `/mnt/d/J2M/builds`에 둔다.
