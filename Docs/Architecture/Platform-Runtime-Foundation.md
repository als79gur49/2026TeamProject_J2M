# Platform Runtime Foundation

## 목적과 범위

M7B-1은 store SDK가 없는 공통 application-level runtime을 제공한다. Gameplay, UI, Save, Stage 같은 game system은 선택된 store SDK나 provider package를 직접 소유하지 않는다. 공통 경계는 `Game.Platform.Runtime`이며, 선택적 provider package는 이 assembly를 참조해 factory를 등록할 수 있다.

이번 범위에 포함되는 것은 공통 runtime 계약, Local provider, factory registry와 selection, Unity application lifecycle host, internal test seam, neutrality guard다. Cloud, achievements, stats, overlay, rich presence, entitlement, user identity, friends, leaderboard, input 같은 기능 port와 store별 provider는 요구가 생기는 후속 단계에서 별도로 설계한다.

## Core와 optional provider 경계

의존 방향은 항상 다음과 같다.

```text
Gameplay / UI / Save / Stage
              ↓
      Game.Platform.Runtime
              ↑
      optional provider package
```

`Game.Platform.Runtime`은 provider SDK, provider managed assembly, native library를 참조하지 않는다. Core가 미리 정의하는 provider ID는 `local`뿐이며, 새 provider는 Core enum이나 source를 수정하지 않고 자신의 `IPlatformRuntimeFactory`를 등록한다.

Provider package는 물리적으로 제거 가능한 선택적 경계다. 제거 후 factory registration이 사라져도 명시적인 provider 요청이 없으면 Local runtime이 선택되며, Gameplay/UI/Save/Stage와 Platform Core source 수정은 필요하지 않아야 한다. 반대로 제거된 provider를 명시적으로 요청하면 `RequestedProviderNotRegistered`로 fail closed하며 Local로 대체하지 않는다.

## Local provider

`LocalPlatformRuntime`은 실패 fallback이나 null object가 아니라 다음 환경을 지원하는 production provider다.

- 직접 배포 및 DRM-free build
- provider SDK가 없는 Windows Player
- 자동화된 local 실행 환경

Local은 `ProviderId == local`, available 상태, 성공하는 idempotent initialization, no-op tick, idempotent shutdown을 제공한다. 선택된 vendor provider의 initialization 실패를 Local 성공으로 바꾸는 데 사용하지 않는다.

## Generic provider selection

`-j2mPlatformProvider <provider-id>`의 parsing과 request state는 항상 compile되는 `Game.Platform.Runtime`이 소유한다. Optional provider와 SDK adapter는 generic argument를 읽거나 unknown provider를 판정하지 않는다.

`PlatformProviderSelection`은 `SubsystemRegistration`에서 process arguments를 읽고 다음 request kind 중 하나를 보존한다.

- `None`: provider argument가 없음
- `Explicit`: trim 및 lowercase canonicalization을 통과한 provider ID 하나
- `Invalid`: missing/empty/invalid provider ID
- `Conflicting`: 동일 값 여부와 무관하게 provider argument가 둘 이상

지원 형식은 기존의 `-j2mPlatformProvider steam`과 `-j2mPlatformProvider=steam`이다. Request에는 source와 canonical provider ID만 저장하며 전체 command line이나 raw invalid token은 저장하지 않는다. `SubsystemRegistration`과 test reset은 selection, registry, cached diagnostics, host owner를 함께 초기화해 domain reload 또는 test ordering의 stale intent를 막는다.

## Registry와 resolution

`PlatformRuntimeRegistry`는 bootstrap용 factory registry이며 runtime 또는 feature service를 조회하는 service locator가 아니다. 선택된 runtime의 lifecycle 소유권은 application host에만 있다.

| Request | Matching factory | 결과 | Local fallback |
| --- | --- | --- | --- |
| `None` | 무관 | `DefaultLocalSelected` | 사용 |
| `Explicit(local)` | built-in Local | `ExplicitProviderSelected` | 미사용 |
| `Explicit(provider)` | 있음 | matching runtime만 생성 | 미사용 |
| `Explicit(provider)` | 없음 | `RequestedProviderNotRegistered` | 금지 |
| `Invalid` | 무관 | `InvalidProviderSelection` | 금지 |
| `Conflicting` | 무관 | `ConflictingProviderSelection` | 금지 |

동일 ID의 중복 등록과 seal 이후 late registration은 명시적 오류다. Registry는 selection 전에 seal되며, 여러 factory가 있어도 explicit request와 정확히 일치한 factory 하나만 생성한다. Factory exception, null runtime, factory/runtime provider ID 불일치는 selection failure로 containment한다. 이 모든 실패에서 Local runtime을 채워 성공처럼 표시하지 않는다.

`PlatformRuntimeSelectionResult`는 selection kind/source, requested/selected provider ID, status, registered provider count, requested match 여부, `FallbackUsed`, failure reason을 제공한다. Default Local과 explicit Local은 각각 `DefaultLocalSelected`와 `ExplicitProviderSelected`로 구분된다.

Factory가 등록되어 runtime selection에 성공했지만 initialization 또는 availability가 실패하면 host-owned final resolution은 `RequestedProviderUnavailable`이 된다. 이 상태는 `RequestedProviderNotRegistered`와 다르며, 선택된 provider identity와 runtime을 partial cleanup용으로 유지하되 tick을 시작하지 않고 Local fallback을 사용하지 않는다.

## Unity lifecycle

초기화 phase는 assembly 간 같은 phase의 실행 순서에 의존하지 않도록 분리한다.

```text
SubsystemRegistration
  generic argument parse
  selection, registry, host owner, diagnostics, test override reset

AfterAssembliesLoaded
  optional provider adapter factory self-registration

BeforeSceneLoad
  override validation → registry seal → selection
  → runtime-created host → initialization attempt
```

Batch mode도 production 실행 경로일 수 있으므로 `Application.isBatchMode`로 bootstrap을 끄지 않는다.

## Application host ownership

`PlatformRuntimeApplicationHost`는 고정 이름의 runtime-created `GameObject`에 생성되고 `DontDestroyOnLoad`로 유지된다. Scene installer, Prefab, ScriptableObject 또는 `Resources` object를 요구하지 않는다.

Canonical host 하나만 selection result, runtime, initialization result, tick eligibility, shutdown state를 소유한다. Duplicate host는 lifecycle 소유권을 얻지 않고 제거된다.

- Initialization은 선택 성공 runtime에 정확히 한 번 시도한다.
- Initialization failure나 exception은 해당 provider identity와 진단을 유지하며 tick을 허용하지 않는다.
- Tick은 initialization success 이후 shutdown 전까지만 실행한다.
- Tick exception은 containment 후 fail-closed 처리해 매 frame 반복 실행과 반복 로그를 막는다.
- `OnApplicationQuit`과 `OnDestroy`는 같은 shutdown-once 경로로 수렴한다.
- Runtime이 만들어졌다면 initialization success 여부와 무관하게 partial initialization 정리를 한 번 시도한다.

## Internal test bootstrap seam

PlayMode test는 `InternalsVisibleTo`로만 노출된 seam을 통해 production auto-bootstrap suppression 또는 fake factory injection을 설정한다. Fake도 registry selection과 정상 host lifecycle을 통과한다.

다음 조합은 invalid다.

- suppression과 fake injection 동시 설정
- registry seal 이후 override
- host initialization 이후 override

`SubsystemRegistration` reset은 override를 모두 제거해 domain reload 설정과 test ordering 의존을 없앤다. 이 seam은 public production API가 아니다.

## Serialization neutrality

Platform runtime owner는 실행 중에 생성한다. M7B-1은 Scene, Prefab, ScriptableObject asset, `Resources`, Addressables, ProjectSettings에 provider object나 provider typename을 직렬화하지 않는다.

Architecture guard는 production Platform Core source/asmdef의 vendor token과 dependency 방향, runtime initialize phase, `AlwaysLinkAssembly`, public service-locator 형태 API 부재를 검사한다. Serialized runtime asset 검사는 Platform Core script GUID와 provider-specific managed typename/object가 없는 현재 상태를 고정한다. 문서와 test fixture의 설명용 provider 용어는 production Core token guard 범위가 아니다.

## Runtime provider와 distribution target

Runtime provider 선택과 distribution artifact 구성은 별도 문제다. M7B-1은 runtime selection만 제공하며 다음을 구성하지 않는다.

- DirectWindows 또는 store별 distribution profile
- build-time provider selection
- native payload 포함/제외
- store upload, depot, manifest, provenance, promotion

Distribution 정책과 store artifact 검증은 후속 milestone에서 다룬다.

## 제거 정책과 후속 단계

Provider package 제거 검증은 optional adapter와 SDK/native payload를 물리적으로 제거한 뒤, 공통 source 수정 없이 Local Player와 focused/core lane이 유지되는지를 확인해야 한다. Source-only Steam lifecycle package는 SDK/native 없이 유지될 수 있으며, physical removal validation은 no-selection Local success뿐 아니라 removed adapter에 대한 explicit Steam request의 `RequestedProviderNotRegistered`도 함께 확인한다.

```text
STEAM_PROVIDER_PACKAGE_REMOVAL_FEASIBLE
STEAM_PHYSICAL_REMOVAL_TEST_DEFINED
```

Steamworks.NET dependency와 native payload의 delivery/license gate는 이 source-only 범위에서 승인되지 않았다. Steam distribution, SteamPipe, VDF/depot과 store artifact promotion은 후속 distribution 범위로 남긴다.

이 main-based semantic port의 validated reference lineage는 `608c5badf6a8f3b7de7179d6f0b87d892d60f461`이다. Commit ancestry는 이식하지 않고 현재 main에서 필요한 contract 의미만 이식한다.
