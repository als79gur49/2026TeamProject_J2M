# BGM Flow v1 Guidelines

이 문서는 persistent BGM ownership v1의 active supporting truth-source다.

이 문서는 gameplay one-shot audio scope를 넓히지 않은 채, scene request source와 persistent BGM owner의 책임을 고정한다. v1의 목표는 ownership continuity, cross-scene lifetime hardening, 그리고 single-source `FadeOutIn` execution이다.

## 1. Ownership Terms

- `persistent runtime owner`
  - `GlobalAudioFlowRoot`를 뜻한다.
  - persistent shared audio runtime과 `BgmFlowCoordinator`를 함께 소유한다.
- `scene-local installer access seam`
  - canonical scene bootstrap root에 co-located 된 `AudioRuntimeInstaller`를 뜻한다.
  - gameplay/UI는 계속 이 same-root seam을 통해 audio settings/runtime access를 얻는다.
  - persistent runtime이 있을 때도 scene-local installer가 항상 creator/owner인 것은 아니다.
- `scene request source`
  - `SceneBgmRequestSource`를 뜻한다.
  - scene entry에서 원하는 `BgmProfile` claim lease를 획득하고 scene lifetime 동안 보유하며 `OnDestroy`에서 해제한다.
  - `IAudioService`, `PlayBgm`, `StopBgm`를 직접 호출하지 않는다.
- `stage audio request source`
  - `StageAudioRuntimeRequestSource`를 뜻한다.
  - resolved stage audio metadata를 Profile 또는 explicit Stop claim으로 변환하고 acquired lease를 반환하는 stateless translator다.
  - `StageBackedGameplaySceneInstallerBase`가 반환된 lease를 stage lifetime 동안 보유하며 teardown에서 해제한다.
  - `IAudioService`, `PlayBgm`, `StopBgm`를 직접 호출하지 않는다.

## 2. Core Invariants

- BGM은 flow-owned이고 gameplay-owned가 아니다.
- scene object는 requester일 뿐이고 long-lived owner가 아니다.
- router의 request는 transient signal이 아니라 owner-scoped persistent claim이다. 모든 production requester는 `Acquire`가 반환한 idempotent lease를 보유해야 한다.
- router는 source kind마다 claim 하나만 유지한다. 새 same-kind acquisition은 이전 slot을 대체하며, 이전 token의 늦은 dispose는 새 claim을 제거하거나 대체된 claim을 부활시키지 않는다.
- claim validity를 lifecycle ownership으로 먼저 결정한 뒤, 남은 유효 claim 중 priority로 selection을 정한다.
- 마지막 claim 철회는 router selection만 비운다. 현재 playback을 중단하는 authoritative silence는 유효한 explicit Stop claim으로만 요청한다.
- persistent `BgmFlowCoordinator`만 current BGM continuity policy를 소유한다.
- shared audio runtime은 playback/mixing/settings-response infrastructure만 소유한다.
- gameplay one-shot SFX는 계속 `GameplayAudioMap -> GameplayAudioPresentationController -> IGameplayAudioPlaybackPort` path에 남는다.
- `GameplayAudioPresentationController`, `GameplayAudioMap`, `GameplaySceneHostConfiguration`는 BGM ownership을 얻지 않는다.
- scene-global fallback lookup은 금지한다.
- `StageAudioDefinition`은 content metadata / playback profile reference owner일 뿐이고 BGM execution owner가 아니다.
- StageAudioDefinition v1 supports only gameplay BGM. Stage result/failure BGM, boss/objective phase BGM, preview/menu BGM, ambience, and layered music are intentionally out of scope and not modeled.
- stage-backed gameplay BGM은 `StageAudioRuntimeRequestSource -> BgmRequestRouter -> BgmFlowCoordinator` path로만 실행한다.
- presentation-exclusive audio가 현재 BGM을 일시 정지해야 할 때도 `BgmRequestRouter`의 playback-suppression lease를 사용한다. feature composition은 `BgmFlowRequest`, 선택된 profile, 또는 priority를 캡처하거나 재제출하지 않는다.

## 3. Bootstrap And Registry Contract

- `AudioRuntimeExternalRootRegistry`는 composition/bootstrap plumbing only다.
- 이 registry는 general-purpose service locator가 아니다.
- register owner:
  - `GlobalAudioFlowRoot`만 register / unregister 할 수 있다.
- runtime reader:
  - runtime code에서는 `AudioRuntimeInstaller`만 registry를 읽는다.
- single-slot rule:
  - active externally-registered persistent audio runtime은 최대 하나다.
- duplicate registration rule:
  - second registration은 fail fast 한다.
  - exact message:
    - `AudioRuntimeExternalRootRegistry cannot register multiple persistent AudioRuntimeRoot instances.`
- teardown rule:
  - unregister는 same owner token + same `AudioRuntimeRoot` 조합으로만 허용한다.
  - play mode / domain reload-sensitive path에서는 subsystem registration reset으로 static state를 비운다.

## 4. Canonical Bootstrap Path

- canonical persistent-flow scene는 `UIAudioScene`이다.
- canonical scene bootstrap root에는 아래 두 component가 함께 있어야 한다.
  - `AudioRuntimeInstaller`
  - `GlobalAudioFlowBootstrap`
- canonical scene installer mode는 `PreferRegisteredPersistentRuntime`로 고정한다.
- behavior는 아래로 고정한다.
  - registered persistent runtime이 있으면 scene-local installer가 그 runtime을 expose 한다.
  - registered persistent runtime이 없으면 historical local child-root creation path로 fall back 한다.
- non-canonical scene는 default `LocalOnly` behavior를 유지한다.

repair notes:

- `GlobalAudioFlowBootstrap`는 canonical bootstrap root에 둔다.
- `SceneBgmRequestSource`는 owner가 아니라 requester이므로 bootstrap root를 대체하면 안 된다.
- stage-backed scene에서 gameplay BGM이 필요하면 `SceneBgmRequestSource`를 활성화하지 않고 `StageAudioDefinition` companion을 작성한다.
- wiring 문제가 생겨도 scene-global search helper를 추가해서 고치면 안 된다.

exact fail-fast messages:

- `SceneBgmRequestSource requires a serialized GlobalAudioFlowBootstrap reference when a BgmProfile is assigned.`
- `GlobalAudioFlowBootstrap could not provide a persistent BGM flow coordinator. Verify the persistent audio-flow bootstrap path; scene-global lookup is not supported.`
- `GlobalAudioFlowBootstrap requires a co-located AudioRuntimeInstaller configured for PreferRegisteredPersistentRuntime.`
- `GlobalAudioFlowRoot cannot exist more than once. Reuse the existing persistent audio-flow root instead of creating another.`

## 5. Transition Governance

- `Immediate` and `FadeOutIn` are executed transition modes in BGM flow v1.
- `Crossfade` is reserved for a future multi-source BGM runtime.
- `FadeOutIn` execution belongs to the BGM flow/shared audio runtime path, not to request sources, visual adapters, or menu installers.
- v1 runtime behavior는 아래로 고정한다.
  - `Immediate`는 기존처럼 즉시 stop/start 한다.
  - `FadeOutIn`은 single BGM source에서 fade out -> switch -> fade in으로 실행한다.
  - `Crossfade` request는 warning 하나를 남기고 fallback 한다.
  - fallback policy:
    - profile fade duration 중 하나라도 0보다 크면 `FadeOutIn`
    - fade duration이 모두 0이면 `Immediate`
  - exact warning:
    - `BgmProfile '<ProfileName>' requests Crossfade, but single-source BGM runtime does not support Crossfade. Falling back to <FallbackMode>.`
- `BgmProfile` inspector/authoring guidance:
  - `loopDefinition` must be non-null
  - `loopDefinition` must use `AudioCategory.Bgm`
  - `fadeOutSeconds` / `fadeInSeconds` must be finite and greater than or equal to zero
  - transition dropdown의 `Crossfade`는 future policy reservation이며 현재 runtime behavior 완성을 의미하지 않는다

## 6. Policy Vs Playback Capability Roadmap

- coordinator는 continuity와 transition policy decision만 소유한다.
- playback port는 `Play(BgmPlaybackRequest)` / `Stop(BgmStopRequest)` request를 shared runtime capability로 전달하는 seam이다.
- true `FadeOutIn` is supported by the request-based playback port and shared audio runtime.
- true `Crossfade` needs shared-runtime multi-lane/capability expansion beyond the current single BGM lane.
- `BgmFlowCoordinator`는 low-level timing/mixing mechanics를 직접 소유하지 않는다.
- source priority는 `BgmRequestRouter`가 소유한다: `SceneDefault=100`, `StageGameplay=300`.
- `BgmFlowCoordinator`는 router에서 선택된 request만 실행한다.

### 6.1 Temporary Playback Suppression

- `BgmRequestRouter.BeginPlaybackSuppression()`은 현재 playback을 정지하고 한 개의 active lease를 소유한다. 동시 lease는 setup defect로 fail fast 한다.
- suppression 중에도 scene/stage requester는 동일한 `Acquire`/lease 계약을 유지한다. claim 등록과 철회는 최신 최우선 selection만 갱신하고 coordinator에 실행하지 않는다.
- cancellation, setup failure, stale callback, immediate route rejection처럼 source scene에 남는 종료는 lease dispose로 수렴한다. router는 캡처된 과거 request가 아니라 종료 시점의 현재 최우선 request를 다시 계산해 실행한다.
- destination route가 동기적으로 승인된 종료만 `ReleaseWithoutRestore()`를 사용한다. 이전 scene BGM은 재시작하지 않고, suppression 해제 뒤 destination requester의 다음 claim acquisition만 playback을 실행한다. 그 사이의 claim 철회는 selection만 갱신한다.
- 이 lease는 `ComicSequence` 같은 새 global request source kind를 만들지 않으며 `IBgmPlaybackPort`의 `Play` / `Stop` surface도 넓히지 않는다.
- 동기 승인 뒤의 비동기 scene-load failure 복구는 scene-transition owner의 별도 책임이며 comic audio-focus 종료가 완료를 주장하지 않는다.

forward plan:

- phase 1: `Immediate` continuity
- phase 2: request-based `FadeOutIn` execution on one BGM source
- phase 3: true `Crossfade` via multi-lane runtime expansion

## 7. Reporting Scope

- v1 ownership continuity와 single-source `FadeOutIn` support는 true `Crossfade` support와 동일하지 않다.
- reporting은 실제 실행한 검증 범위만 말해야 한다.
- approved reporting levels:
  - `build verified`
  - `targeted persistent BGM ownership validated`
  - `cross-scene continuity validated`
  - `real transition-effects validation completed`
- approved sentence template:
  - `Persistent BGM ownership, cross-scene continuity, and single-source FadeOutIn are validated; Crossfade remains reserved.`
