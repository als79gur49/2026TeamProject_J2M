# Audio Architecture Guidelines

이 문서는 현재 프로젝트의 active audio architecture supporting truth-source다.

이 문서는 gameplay authoritative boundary, UI canonical mapped presentation path, runtime ownership rule을 유지한 채 현재 저장소에 필요한 2D non-spatial audio structure를 고정한다. 새 오디오 시스템을 다시 설계하지 않고, archived blueprint를 active architecture 기준에 맞게 정렬하는 것이 목적이다.

## 1. Relationship To Active Architecture

이 문서는 아래 active truth-source를 확장한다.

- [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
- [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)
- [ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md](./ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md)
- [Gameplay-Audio-Governance.md](./Gameplay-Audio-Governance.md)
- [Gameplay-Action-Audio-Governance.md](./Gameplay-Action-Audio-Governance.md)
- [Bgm-Flow-V1-Guidelines.md](./Bgm-Flow-V1-Guidelines.md)

Conflict rule:

- gameplay authoritative ownership은 gameplay canonical docs가 우선한다.
- UI mapped presentation path ownership은 UI canonical doc이 우선한다.
- 이 문서는 그 위에서 audio projection boundary, runtime ownership, and 2D-only contract만 추가로 고정한다.

Historical predecessor:

- [Docs/Archive/Architecture/Unity-Audio-System-Blueprint.md](../Archive/Architecture/Unity-Audio-System-Blueprint.md)

## 2. Core Invariants

- `WorldState`, `TickPipeline`, `Committer`, `EntityLogic`는 오디오를 직접 재생하지 않는다.
- 오디오는 gameplay 결과를 소비하는 presentation concern이다.
- shared sound definition과 feature semantic mapping은 분리한다.
- feature는 `IAudioService` 계약에만 의존하고 `AudioManager` 구현 세부를 모른다.
- 오디오 때문에 gameplay-core 전용 버스를 새로 만들지 않는다.
- 오디오는 UI frozen baseline과 경쟁하는 별도 presentation 체계를 만들지 않는다.
- 이 프로젝트의 audio layer는 2D non-spatial only다.

## 3. Public Seam Vocabulary

공식 용어는 아래 둘만 사용한다.

- `Authoritative Presentation Signal Seam`
  - `TickResult.PresentationData`와 public final presentation seams
  - gameplay-owned public presentation layer
  - audio, view, future non-UI projection consumer가 읽는 층
- `Mapped Presentation Seam`
  - `GameplayUiPresentationSource -> UITickEventRouter -> UIStateMapper -> UIPresentationSnapshot`
  - UI application-owned mapped path
  - HUD/Popup/Screen이 읽는 층

`GameplayPresentationFrame`과 `IGameplayPresentationFeed`는 UI-access projection intermediary다. Audio는 이 UI 전용 intermediary를 재사용하지 않는다.

```text
TickResult
  -> TickPresentationData + public final seams
     [Authoritative Presentation Signal Seam]
      -> GameplayTickViewPresenter
          -> GameplayTickPresentationCoordinator
              -> GameplayAudioRequestPlanner
              -> GameplayAudioPresentationController
              -> GameplayAudioMap
              -> IGameplayAudioPlaybackPort
                  -> IAudioService
      -> GameplayHostPresentationFeed
          -> GameplayUiPresentationSource
              -> UITickEventRouter
              -> UIStateMapper
                  -> UIPresentationSnapshot
                     [Mapped Presentation Seam]
```

## 4. Layer Responsibilities

- `AudioDefinition`
  - sound identity only
  - clip/variant, category, default volume trim, pitch or pitch range, loop
- `AudioBinding`
  - feature semantic -> definition binding owner
  - optional attached slot metadata
  - optional future `AudioPlaybackPolicy` seam
  - binding-local validation rule의 canonical owner
- `GameplayAudioMap`
  - typed feature semantic dictionary
  - authoring 단계에서는 visible validation error를 남기고, bootstrap/runtime에서는 hard-fail 한다
  - binding-local validation은 직접 재구현하지 않고 delegated `AudioBindingDiagnostics`를 수집한다
  - feature map consumer는 delegated `AudioBinding` diagnostics를 집계한다
  - validation authority reuse의 immediate policy는 deferred다
  - shared `AudioBindingDiagnostics` facade는 binding-local validation reuse seam이다
- `GameplayAudioSemanticId`
  - core required gameplay one-shot semantic vocabulary의 canonical typed id
  - raw string semantic literal을 대체한다
  - push/flip/action-specific SFX를 흡수하는 expanding global vocabulary가 아니다
- `GameplayAudioSemanticCatalog`
  - semantic descriptor metadata의 canonical owner
  - `RequiredOneShotV1`는 descriptor metadata에서 derive된다
  - planner emission vocabulary, bootstrap validation, tests, logs/error formatting에 공통 사용된다
  - allowed/disallowed family governance는 [Gameplay-Audio-Governance.md](./Gameplay-Audio-Governance.md) 가 canonical owner다
- `GameplayAudioRequestPlanner`
  - `TickResult.PresentationData`와 public final seams만 읽는다
  - supported damage/exit presentation fact를 typed gameplay audio request로 변환한다
  - `GameplayAudioMap`, `IAudioService`, owner view resolution, continuous handle state를 소유하지 않는다
  - allowed family는 `DamageOneShot`, `EntityExitOneShot`뿐이다
  - locomotion loop, jump loop, windup/recovery loop, ambient gameplay bed, UI audio, BGM은 intentionally excluded v1 scope다
- `GameplayActionKind` / `GameplayActionAudioMoment`
  - gameplay action-audio profile-local typed authoring axes다
  - global required gameplay semantic IDs가 아니다
- `GameplayActionAudioProfile`
  - prefab-local action + moment -> `AudioBinding` authoring asset
  - duplicate detection, category/loop validation, optional/required policy만 소유한다
  - global completeness governance를 소유하지 않는다
- `GameplayActionAudioRequestPlanner`
  - existing `PlayerActionSignals`를 frozen v1 action moments로 매핑한다
  - `TickResult`에 audio-specific data를 추가하지 않는다
  - same-tick duplicates를 suppress하지 않고 order-preserving layering을 유지한다
- `GameplayActionAudioPresentationController`
  - host-owned presentation-side controller다
  - live owner view에서 optional `GameplayActionAudioAuthoring`를 resolve한다
  - missing owner view / missing authoring은 runtime no-op다
  - looping handle state나 enemy reaction governance를 소유하지 않는다
- `EnemyChargeLoopAudioPresentationController`
  - host-owned presentation-side controller다
  - `TickEnemyChargePresentationSignal`의 `Active` phase를 desired persistent state로 읽는다
  - enemy-local `EnemyAudioProfile`의 `ChargeActiveLoop` binding을 attached loop로 재생하고 handle을 소유한다
  - active phase 이탈, sequence 변경, session reset, runtime detach에서 handle을 정지한다
  - core required gameplay semantic set이나 action-audio moment set을 확장하지 않는다
- `EnemyAudioRequirementPolicy` / `EnemyAudioRequirementBinding`
  - archetype-level policy와 sparse profile binding으로 prefab-local enemy `EnemyAudioProfile_*`의 runtime can-emit cue를 governance한다
  - policy는 `Required` / `Optional` cue만 명시하며 unspecified runtime cue는 implicit `Disabled`다
  - validation은 `EnemyAudioCueCatalog.RuntimeCues` 전체를 순회한다
  - required cue missing binding은 production content error다
  - optional cue missing binding은 intentional no-op다
  - optional cue with an authored invalid binding is still a validation error
  - disabled cue는 binding을 가지면 안 된다
  - runtime missing owner/authoring/profile/cue no-op policy는 변경하지 않는다
  - Startis는 `PassiveContact`를 production required content로 authoring하므로, 기존에 content 누락으로 no-op였던 `PassiveContact` signal은 이제 authored cue를 재생할 수 있다
  - `GameplayPresentationAudioConfig`에 포함되지 않는다
- `GameplayAudioPresentationController`
  - host-owned orchestration controller다
  - `GameplayTickPresentationCoordinator` 내부 collaborator로 존재한다
  - precomputed typed request list만 받는다
  - typed request를 `GameplayAudioMap`과 `IGameplayAudioPlaybackPort`를 통해 runtime playback으로 내린다
  - missing owner view는 failure가 아니라 `Play2D` fallback으로 degrade한다
  - gameplay presentation one-shot audio 외의 domain은 다루지 않는다
- `IGameplayAudioPlaybackPort`
  - gameplay host-local narrow playback port다
  - `Play2D`와 `PlayAttached`만 가진다
  - gameplay host orchestration path에서 `PlayBgm`을 compile-time으로 차단한다
- `IAudioService`
  - feature-facing playback contract
- `IBgmPlaybackPort`
  - flow-owned BGM continuity policy가 shared runtime capability에 닿는 narrow execution seam이다
  - v1에서는 `Play(BgmPlaybackRequest)`와 `Stop(BgmStopRequest)`만 허용한다
  - `FadeOutIn`은 coordinator timing logic이 아니라 shared audio runtime playback capability로 실행한다
- `AudioManager`
  - central runtime implementation
  - definition resolution, source lease, attached registry delegation, BGM routing만 담당한다
  - feature semantic branching 금지
  - runtime root initialization 이전 service entrypoint 호출은 setup defect로 즉시 실패한다

### 4.1 Gameplay Host-Orchestration Contract

- gameplay-origin one-shot SFX는 `GameplayTickPresentationCoordinator`가 orchestration owner다.
- canonical ordering은 아래 exact sequence로 고정한다.
  1. `RefreshAudioPlan(result)`
  2. `RefreshUtilityWindupWarnings()`
  3. `RefreshFrontFaceShieldSources()`
  4. `PlayPlannedAudio()`
  5. `ApplyEntityExitOwnership()`
- `RefreshAudioPlan(result)` 내부에서는 core gameplay one-shot plan과 action-audio plan을 함께 refresh한다.
- RefreshAudioPlan(result) 내부에서는 core gameplay one-shot plan과 action-audio plan을 함께 refresh한다.
- `PlayPlannedAudio()` 내부에서는 core gameplay one-shot requests를 먼저 실행하고, 그 다음 action-audio requests를 실행한다.
- attached owner resolution은 exit ownership removal 이전에만 수행한다.
- pending gameplay audio plan은 `PresentInitial`, session reset, presenter teardown에서 반드시 비워진다.
- pending gameplay audio plan lifecycle은 아래 rule로 고정한다.
  - `ReplacePendingPlan(...)`은 last-write-wins replacement다.
  - empty replace는 legal이며 pending state를 clear한다.
  - `PlayPlannedAudio()`는 current plan을 한 번만 재생하고 즉시 clear한다.
  - explicit clear는 idempotent다.
- gameplay audio host path는 generic dispatcher가 아니다.
  - UI audio는 UI presenter/controller path에 남는다.
  - BGM/scene-flow audio는 stage/scene flow presenter path에 남는다.
  - gameplay host audio controller는 `PlayBgm`을 호출하지 않는다.
  - core enemy damage/death reaction sounds는 existing core one-shot path에 남는다.
  - GameplayActionAudioMoment v1 no longer includes `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, or `Blocked`; gameplay action execute/recovery timeline facts and impact/blocked gameplay/presentation signals remain outside the action-audio lane.
  - Charge active loop audio remains a separate enemy-local persistent controller, not a core one-shot semantic.
  - persistent BGM ownership/access terminology는 [Bgm-Flow-V1-Guidelines.md](./Bgm-Flow-V1-Guidelines.md) 를 따른다.

### 4.2 Gameplay Audio Bootstrap Validation

- `GameplaySceneHostConfiguration.GameplayPresentationAudioConfig`가 assigned되면 `GameplaySceneHost` canonical host root same `GameObject`에 co-located `AudioRuntimeInstaller`가 있어야 한다.
- `GameplayPresentationAudioConfig`는 typed gameplay host presentation SFX maps를 group하는 data + validation owner다.
- `GameplayPresentationAudioConfig`는 dispatcher, planner, controller factory, service locator, playback owner가 아니다.
- `GameplayPresentationAudioConfig`는 `GameplayAudioMap`, `BlockAudioMap`, `PlayerLocomotionAudioMap`, `TopologyAudioMap`, `GravityFieldAudioMap`, `TileFeatureAudioMap`만 소유한다.
- action/enemy prefab-local profiles, enemy requirement policies/bindings, UI cue maps, BGM profiles, `StageAudioDefinition`, runtime installers, audio settings bridges는 이 config 밖에 남는다.
- `GameplayHostRuntimeFactory`는 scene-global lookup을 하지 않는다.
- missing installer fail-fast message는 아래 exact string으로 고정한다.
  - `GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when GameplayPresentationAudioConfig is assigned.`
- `GameplayAudioMap.ValidateRequiredSemanticsOrThrow(GameplayAudioSemanticCatalog.RequiredOneShotV1)`는 host attach/init에서 first gameplay playback 이전에 수행되어야 한다.
- missing required semantic fail-fast message는 아래 format으로 고정한다.
  - `GameplayAudioMap '<MapName>' is missing required gameplay audio semantics: <Id1>, <Id2>.`
- semantic family growth protocol과 required-set review checklist는 [Gameplay-Audio-Governance.md](./Gameplay-Audio-Governance.md) 를 따른다.

### 4.3 Persistent BGM Flow Boundary

- `persistent runtime owner`는 `GlobalAudioFlowRoot`다.
- `scene-local installer access seam`은 canonical root same `GameObject`의 `AudioRuntimeInstaller`다.
- scene-local installer는 persistent runtime이 있을 때 creator/owner가 아니라 access seam만 제공할 수 있다.
- `AudioRuntimeExternalRootRegistry`는 bootstrap plumbing only이며 service locator가 아니다.
- `Immediate` and `FadeOutIn` are executed transition modes in BGM flow v1.
- true `FadeOutIn` is supported by request-based playback-port/runtime support.
- true `Crossfade` needs shared-runtime multi-lane/capability expansion beyond the current single BGM lane.
- 이 분리는 coordinator policy와 playback capability roadmap을 분리하기 위한 것이다.
- stage gameplay BGM metadata는 `StageAudioDefinition.gameplayBgm` companion field가 direct `BgmProfile` reference로 소유한다.
- StageAudioDefinition v1 supports only gameplay BGM. Stage result/failure BGM, boss/objective phase BGM, preview/menu BGM, ambience, and layered music are intentionally out of scope and not modeled.
- stage gameplay BGM request는 `StageAudioRuntimeRequestSource -> BgmRequestRouter -> BgmFlowCoordinator` path로만 실행한다.
- stage content, scene installers, visual adapters는 `IAudioService.PlayBgm`를 직접 호출하지 않는다.
- `BgmRequestRouter` priority는 `SceneDefault=100`, `StageGameplay=300`으로 고정한다.

## 5. 2D-Only Playback Contract

Public API는 아래만 허용한다.

- `Play2D`
- `PlayAttached`
- `PlayBgm`
- `Stop`
- `StopBgm`

v1 public contract는 request-based BGM transition을 포함한다.
`FadeOutIn`은 supported이고, `Crossfade`는 future multi-source runtime 확장으로만 도입한다.

금지:

- `Play3D`
- spatial blend
- distance attenuation
- min/max distance
- 2D/3D flag
- follow-target 3D playback
- world-position-based audio trigger contract

`AudioPlaybackContext`는 spatial 정보를 갖지 않는다.

- `VolumeMultiplier`
- `PitchMultiplier`
- `OwnerEntityId`
- `DebugTag`

`AudioPlaybackData`는 non-spatial source config만 가진다.

- `Clip`
- `Category`
- `Volume`
- `Pitch`
- `Loop`

Unity runtime source는 항상 non-spatial 2D source로 구성한다.

## 6. Definition, Binding, And Policy Boundary

`AudioDefinition`은 identity만 소유한다. semantic arbitration은 소유하지 않는다.

금지:

- definition-level cooldown
- definition-level concurrency
- definition-level replace priority
- definition-level policy inheritance
- per-clip policy override chain

future policy rule:

- feature-level arbitration은 `AudioBinding`이 소유한다.
- `AudioManager`는 infra-level global policy만 가진다.
- `AudioPlaybackPolicy`는 `AudioBinding.Policy` reserved seam에만 둔다.
- v1에서는 `AudioBinding.Policy`가 reserved seam이며 반드시 `null`이어야 한다.
- `AudioManager`가 가질 수 있는 policy는 pool size, category voice budget, BGM channel, source stealing rule 같은 infra-level rule뿐이다.
- binding-local validation rule 추가는 `AudioBindingDiagnostics`만 수정한다.
- `AudioBindingDiagnostics`는 binding-local concerns만 알고 category/loop policy는 caller가 전달한다.
- public `AudioBinding` surface는 feature policy hub로 넓히지 않는다.
- 새 feature map은 binding-local rule을 직접 재구현하지 않는다. 권위는 shared에, 조합은 feature에 둔다.

## 7. PlayAttached Contract

`PlayAttached`는 3D follow의 대체가 아니다.

공식 정의:

- non-spatial `Owner-Bound Persistent Playback`

보장:

- `(owner, slot)` 단위 dedupe
- explicit stop 또는 owner invalidation 시 정지
- source lease cleanup과 pool 반환

비보장:

- 위치 추적
- panning
- attenuation
- spatial follow
- disabled owner auto-resume

규칙:

- 같은 `(owner, slot)` 중복 attached는 금지
- 같은 owner라도 slot이 다르면 공존 가능
- same definition 재요청은 existing handle reuse
- 다른 definition 재요청은 replace
- restart는 explicit stop 후 재호출만 허용
- owner disable / inactive / destroy / despawn은 stop cause
- emitter는 semantic start와 explicit stop만 결정
- registry와 manager가 dedupe, cleanup, source lease를 책임진다

## 8. Runtime Ownership

`AudioManager`는 self-bootstrap 하지 않는다.
runtime root initialization 이전 manager service call은 setup defect이며 `InvalidOperationException`을 던진다.

Canonical ownership:

- `Audio Runtime Installer`
  - bootstrap root에 붙는 thin composition component
  - 생성 책임과 dependency wiring만 가진다
- `Audio Runtime Root`
  - installer가 생성하는 runtime-owned infrastructure root
  - `AudioManager`와 runtime-owned support objects를 소유한다
- feature presenter / emitter
  - installer/root를 만들지 않는다
  - `IAudioService`만 주입받는다

future extension note:

- v1 action audio defaulting은 prefab-local authoring only다.
- later stage-wide/default action audio가 필요하면 `GameplaySceneHostConfiguration` 또는 `StagePresentationDefinition`에 ad-hoc audio field를 늘리지 않는다.
- BGM profile metadata는 `StageAudioDefinition`에만 둔다. `StagePresentationDefinition`은 BGM을 소유하지 않는다.
- gameplay host presentation SFX map growth는 grouped `GameplayPresentationAudioConfig`를 통해 관리한다.
- `GameplayPresentationAudioConfig`는 action/enemy prefab-local profiles, enemy requirement policies/bindings, UI cue maps, BGM/stage audio metadata, runtime installers, settings bridges를 소유하지 않는다.

금지:

- `Ensure AudioManager`
- lazy singleton
- global instance search
- feature-side runtime root creation

### 8.1 Audio-Settings Bridge Mapping

- visible settings UI는 `Main`, `Background Music`, `Effects` 3개 channel만 노출한다.
- `UI.Application`은 shared audio enum을 모르고 `IAudioSettingsPort`만 안다.
- `UI.Composition`은 `UIAudioChannelMapper` 하나만 통해 visible UI enum과 shared runtime enum을 연결한다.
- canonical mapping은 아래 셋뿐이다.
  - `Main -> AudioChannel.Master`
  - `Bgm -> AudioChannel.Bgm`
  - `Sfx -> AudioChannel.Sfx`
- presenter, tests, composition runtime은 이 mapping을 inline `switch` 또는 `if`로 재구현하지 않는다.

### 8.2 Playback Registry Lifecycle

- live playback register는 source 획득, definition resolve, source configure, successful `Play` 이후에만 발생한다.
- live playback unregister는 controller `Stop()` 단일 경로만 canonical owner다.
- natural completion은 controller `Tick()`에서 `Stop()`으로 수렴해야 한다.
- pooled source release는 unregister 이후에만 수행한다.
- every live record는 `leaf AudioChannel`, `base clip volume`, `AudioSource` reference validity, controller validity를 유지한다.
- destroyed source, invalid controller, manager teardown은 stale live record를 남기지 않아야 한다.
- BGM도 별도 lane을 쓰지만 registry lifecycle rule은 동일하다.

### 8.3 Reserved Master Category Rule

- `AudioCategory.Master`는 mixer-only reserved category다.
- `AudioDefinition` authoring category로는 사용할 수 없다.
- rule truth-source는 `AudioDefinitionCategoryRules` 하나다.
- `AudioDefinition.OnValidate()` authoring warning과 runtime defensive validation은 같은 shared rule helper를 호출해야 한다.
- 이 규칙을 authoring path, binding path, runtime path에서 각각 다시 encode하지 않는다.

### 8.4 Canonical Bootstrap Root Contract

- audio-first settings를 노출하는 scene에서 `GameplayUiFlowInstaller`가 존재하면 같은 canonical bootstrap root `GameObject`에 정확히 하나의 `AudioRuntimeInstaller`가 co-located 되어야 한다.
- current canonical scene는 `UIAudioScene`이다.
- 여기서 co-located의 의미는 scene-wide search가 아니라 `GameplayUiFlowInstaller`가 붙은 바로 그 same `GameObject`다.
- missing installer fail-fast message는 아래 exact string으로 고정한다.
  - `GameplayUiFlowInstaller requires a co-located AudioRuntimeInstaller on the canonical bootstrap root for SettingsScreen audio controls.`
- duplicate installer policy:
  - same-root duplicate는 `DisallowMultipleComponent`로 차단한다.
  - canonical scene contract test는 each canonical scene에 installer가 정확히 1개인지 검증한다.
  - scene-global fallback lookup은 금지다.

### 8.5 Immediate Apply And Deferred Flush

- slider/toggle interaction은 runtime gain을 즉시 갱신한다.
- persistence write는 transient drag step마다 수행하지 않는다.
- dirty snapshot은 아래 bounded flush policy로만 저장한다.
  - slider interaction end
  - settings screen close or dispose
  - application pause
  - application quit
- discrete mute toggle은 runtime state를 즉시 바꾸되 drag-step처럼 per-frame persistence write를 만들지 않는다.

### 8.6 Hidden Internal Channels

- internal runtime channel은 `Master`, `Bgm`, `Sfx`, `Ui`, `Voice`, `Ambience`다.
- `Ui`, `Voice`, `Ambience`는 v1에서 user-facing control이 없다.
- hidden channel leaf state는 내부 snapshot에 존재하지만 default `volume=1`, `muted=false`를 유지한다.
- hidden channel은 `Master`에는 반응하지만 `Bgm` 또는 `Sfx` control에는 반응하지 않는다.

### 8.7 UI SFX v1 Hidden Ui-Channel Policy And Ownership Matrix

- UI SFX v1는 hidden `Ui` channel로 route한다.
- hidden `Ui` channel은 `Master`를 따른다. `Sfx` mute/volume을 따라가지 않는다.
- 이 동작은 v1에서 intentional하다. `Sfx`를 mute해도 UI feedback은 계속 들릴 수 있다.
- public `Ui` slider 또는 mute를 Settings에 노출하는 것은 separate future product decision이다. 이번 작업 범위가 아니다.
- UI SFX playback은 `Play2D`만 사용한다. `PlayAttached`, spatial ownership, attachment slot authoring은 금지다.
- UI SFX owner split은 아래 셋뿐이다.
  - flow cue: `UIFlowCoordinator`의 transaction/outcome layer만 재생한다.
  - local widget/HUD cue: screen runtime과 HUD feedback controller가 local presentation interaction에서만 재생한다.
  - transition overlay cue: `SceneTransitionCoordinator`가 scene transition overlay가 표시되는 순간의 transition-kind whitelist만 재생한다.
- `ScreenTransitioned`, `PopupOpened`, `PopupCompleted`는 mechanical lifecycle signal이다. direct audio trigger가 아니다.
- one interaction may contain multiple raw lifecycle deltas but still emit only one cue.
- classifier governance rule:
  - outcome classification prefers user intent over raw delta count or event ordering.
  - 새 flow case가 추가되면 classifier matrix, ownership table, tests를 같은 change에서 함께 갱신한다.
- canonical classifier matrix:

| Root Intent | Recorded Deltas | Outcome | Cue |
| --- | --- | --- | --- |
| `Back` | reverse visible delta가 하나라도 있으면, nested `PopupOpen` restore가 함께 있어도 `NavigateBack` | `NavigateBack` | `NavigateBack` |
| `Back` | reverse delta 없이 forward visible delta만 있으면 | `NavigateForward` | `NavigateForward` |
| `OpenForward` | any real forward visible delta | `NavigateForward` | `NavigateForward` |
| `Confirm` | user-visible confirm completion plus any nested transition | `Confirm` | `Confirm` |
| `Cancel` | user-visible cancel completion plus any nested cleanup | `Cancel` | `Cancel` |
| `SystemPresentation` | `RootScreenSet(GameClear)` | `GameClear` | `GameClear` |
| `SystemPresentation` | `RootScreenSet(StageResult)` | `StageClear` | `StageClear` |
| `SystemPresentation` | `RootScreenSet(LevelFailed)` | `LevelFailed` | `LevelFailed` |
| `SystemPresentation` | explicit whitelist entry가 없으면 | `Silent` | none |

- canonical local-vs-flow ownership truth-source table:

| Interaction | Owner | Result |
| --- | --- | --- |
| `PausePopup.SettingsRequested` | Flow only | one `NavigateForward` |
| `PausePopup.ObjectiveRequested` | Flow only | one `NavigateForward` |
| `Settings.Back` from gameplay origin | Flow only | one `NavigateBack` |
| `Settings.Back` from pause origin | Flow only | one `NavigateBack` even if pause popup reopens |
| `Display Apply` | Flow only | one `NavigateForward` when confirm popup actually opens |
| `Display Revert` | Silent | no cue |
| inventory row/actions/search/filter/sort | Local only | local `Select` only |
| objective tab changes | Local only | local `Select` only |
| popup confirm/resume/reward acknowledge | Flow only | one `Confirm` |
| popup cancel/back-cancel | Flow only | one `Cancel` |
| `Stage clear -> StageResult + Reward popup` | Flow only | one `StageClear` |
| `Final stage clear -> GameClear` | Flow only | one `GameClear` |
| `Level failed -> LevelFailed` | Flow only | one `LevelFailed` |
| `Death retry chance loss` | Transition overlay only | one `ChanceLoss` using retry-failed definition |
| back buttons that trigger real pop | Flow only | one `NavigateBack`, never local + flow |

- user/system policy:
  - user-driven navigation uses the classifier matrix above.
  - `SystemPresentation` emits only explicit result-screen whitelist cues and must not inherit navigation defaults accidentally.
  - `Stage clear -> StageResult + Reward popup` is one system-driven transaction and emits `StageClear`, not navigation or reward-popup audio.
  - `DeathRetryChanceLost` emits `ChanceLoss` from the transition overlay path because campaign retry can launch the next scene before HUD chance deltas are observed.
  - `GameClear/StageClear may share one clip through separate definitions`; Def-level pitch/volume differences are the allowed content variation seam.
  - `LevelFailed/RetryFailed may share one clip through separate definitions`; retry-failed/chance-loss content should stay lower-pitched than terminal level failure.
- transaction/debug rule:
  - internal trace는 root intent, collected deltas, chosen outcome, emitted cue 또는 silence reason을 기록한다.
  - test/debug는 raw event count 대신 transaction trace를 primary evidence로 본다.
- authoring contract:
  - canonical asset는 `UiAudioCueMap_V1.asset` 하나다.
  - every `UiAudioCueId` value는 map에 explicit entry로 모두 존재해야 한다.
  - `AudioCategory.Ui`만 허용한다.
  - looping definition은 금지다.
  - `AudioBinding.Policy`는 null이어야 한다.
  - attachment slot은 비어 있어야 한다.
- temporary asset note:
  - placeholder `Ui` definitions/clips는 wiring과 architecture validation 용도로 허용된다.
  - placeholder clip reuse may make distinct cues sound similar, but that is a content issue rather than the structural cause of duplicate-feeling playback.
  - 이것은 final content polish를 의미하지 않는다.
- reporting scope note:
  - `build verified`
  - `ui lane validated`
  - `targeted UI SFX architecture validated`
- out of scope note:
  - hover, disabled/no-op, backdrop-consume feedback는 v1 shipped scope가 아니다.

## 9. Required Enforcement

EditMode / structure guard:

- no `Play3D`
- no spatial field
- no gameplay-side audio ref
- no UI-mapped seam reuse
- no definition-level policy inheritance
- map 누락 fail-fast
- manager-alone no silent recovery
- v1 public contract uses request-based BGM Play/Stop and supports FadeOutIn
- v1 `AudioBinding.Policy` must remain null

PlayMode / runtime guard:

- single installer/root ownership
- duplicate manager 방지
- attached lifecycle cleanup
- `(owner, slot)` dedupe
- replace/reuse semantics
- pool source return

## 10. Deprecated Terms

아래 표현은 active docs와 code review vocabulary에서 deprecated다.

- `Play3D`
- `follow target`
- `2D/3D 전환`
- `spatial blend`
- `distance attenuation`
- `singleton AudioManager`
- `Ensure AudioManager`
- UI path를 단순 `public seam`으로 부르는 표현

## 11. Maintenance Rule

- audio architecture behavior가 바뀌면 이 문서를 같은 PR에서 갱신한다.
- implementation과 이 문서가 diverge하면 mismatch는 defect다.
- audio는 UI mapped seam을 재사용하는 별도 presentation system으로 확장하지 않는다.
- future infra extensions는 feature-facing API를 먼저 부풀리지 않고 internal collaborator seam에 배치한다.
- `AudioBinding`은 binding-local validation rule authority를 단독 소유한다.
- `GameplayAudioMap`은 map-level validation만 소유하고 delegated binding diagnostics를 집계한다.
- authoring/bootstrap/runtime은 failure mode가 달라도 binding-local rule source는 `AudioBinding` 하나다.
- validation reuse가 실제로 gameplay audio 바깥으로 필요해지면 shared diagnostics facade를 추가하고 friend assembly를 기본 경로로 늘리지 않는다.
- `AudioManager` internal extraction priority는 `BGM lane -> source pool and active controller set -> attached registry` 순서를 기본으로 삼는다.
- FadeOutIn은 request-based BGM runtime에 포함되어 있고, crossfade와 ducking은 `AudioBgmChannel` 또는 `AudioBgmController` 추가 추출 시점에 수용한다.
- category budget, stealing, pause-group 성격의 infra rule은 `AudioSourcePool` 또는 active playback/controller set 추출 시점에 수용한다.
- attached semantic 확장이 실제로 필요해질 때만 `AttachedAudioRegistry`를 별도 collaborator로 분리한다.
- 아래 조건이 생기면 Medium이 아니라 escalation 대상이다: second feature map consumer, 추가 friend assembly 요구, binding validation duplication 재발, 또는 fade/crossfade/ducking/stealing/category budget 중 둘 이상이 같은 `AudioManager.cs` 수정으로 들어오는 경우.
