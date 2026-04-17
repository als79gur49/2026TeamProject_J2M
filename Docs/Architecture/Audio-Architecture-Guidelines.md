# Audio Architecture Guidelines

이 문서는 현재 프로젝트의 active audio architecture supporting truth-source다.

이 문서는 gameplay authoritative boundary, UI canonical mapped presentation path, runtime ownership rule을 유지한 채 현재 저장소에 필요한 2D non-spatial audio structure를 고정한다. 새 오디오 시스템을 다시 설계하지 않고, archived blueprint를 active architecture 기준에 맞게 정렬하는 것이 목적이다.

## 1. Relationship To Active Architecture

이 문서는 아래 active truth-source를 확장한다.

- [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
- [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)
- [ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md](./ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md)

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
      -> GameplayAudioPresenter
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
- `GameplayAudioMap`
  - feature semantic dictionary
  - authoring 단계에서는 visible validation error를 남기고, bootstrap/runtime에서는 hard-fail 한다
- `GameplayAudioPresenter`
  - `TickResult.PresentationData`와 public final seams만 읽는다
  - semantic-to-binding projection과 `IAudioService` 호출만 담당한다
  - `EventLog`, phase-private result, raw world diff 재해석 금지
- `IAudioService`
  - feature-facing playback contract
- `AudioManager`
  - central runtime implementation
  - definition resolution, source lease, attached registry delegation, BGM routing만 담당한다
  - feature semantic branching 금지
  - runtime root initialization 이전 service entrypoint 호출은 setup defect로 즉시 실패한다

## 5. 2D-Only Playback Contract

Public API는 아래만 허용한다.

- `Play2D`
- `PlayAttached`
- `PlayBgm`
- `Stop`
- `StopBgm`

v1 public contract는 fade/crossfade를 포함하지 않는다.
future fade/crossfade는 behavior가 실제로 구현될 때 additive overload 또는 explicit options type으로만 도입한다.

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

금지:

- `Ensure AudioManager`
- lazy singleton
- global instance search
- feature-side runtime root creation

## 9. Required Enforcement

EditMode / structure guard:

- no `Play3D`
- no spatial field
- no gameplay-side audio ref
- no UI-mapped seam reuse
- no definition-level policy inheritance
- map 누락 fail-fast
- manager-alone no silent recovery
- v1 public contract has no fade/crossfade
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
