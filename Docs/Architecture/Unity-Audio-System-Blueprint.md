# Unity Audio System Blueprint

## 1. 목적

이 문서는 본 Unity 프로젝트에서 사용할 공용 오디오 시스템의 최종 설계 기준을 정의한다.

이 설계는 아래 문서의 구조 원칙을 따른다.

- `Docs/Architecture/Hybrid-Architecture-Rulebook.md`
- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`

이 시스템의 핵심 목표는 단순 재생이 아니다.

- 게임 로직이 `AudioClip`과 `AudioSource`를 직접 다루지 않게 한다.
- 사운드 규칙을 `ScriptableObject` 데이터로 관리한다.
- 오브젝트별 오디오 문맥을 `Emitter` 계층으로 캡슐화한다.
- 실제 재생 인프라는 중앙 서비스에서 통제한다.
- 추후 랜덤 선택, 동시 재생 제한, 풀링, BGM 크로스페이드, 옵션 저장/로드를 붙여도 구조를 깨지 않게 한다.

한 줄로 요약하면 다음과 같다.

> 오디오 데이터, 오브젝트 문맥, 실제 재생 인프라를 분리한다.

## 2. 이 프로젝트에 적용할 핵심 제약

이 프로젝트는 결정론적 Tick 시뮬레이션을 사용하므로, 오디오 설계도 이 제약을 반드시 따른다.

- `Gameplay_Loop`, `Gameplay_Movement`, `Gameplay_Attack`, `Gameplay_Cleanup` 같은 시뮬레이션 계층은 오디오를 직접 재생하지 않는다.
- `WorldState`, `TickPipeline`, `Committer`, `EntityLogic`는 `IAudioService`를 참조하지 않는다.
- 오디오 재생은 `Host`, `Presenter`, `View`, 일반 Unity `MonoBehaviour` 계층에서만 발생한다.
- `TickResult.EventLog`는 디버그/리플레이용 문자열 로그이며, 오디오 재생 계약으로 사용하지 않는다.
- Tick 기반 사운드는 별도 `PresentationCue` 계층 또는 Presenter의 상태 변화 해석을 통해 발생시킨다.

중요한 원칙은 다음 한 줄이다.

> 오디오는 시뮬레이션 결과에 반응할 수는 있지만, 시뮬레이션을 구성하거나 되먹임하면 안 된다.

## 3. 최종 계층 구조

이 시스템은 아래 5개 층으로 본다.

### 3-1. 데이터 자산 계층

사운드가 어떤 규칙으로 재생될지를 정의한다.

- `AudioDefinition`
- `SingleAudioDefinition`
- `RandomAudioDefinition`
- `AudioBusConfiguration`

### 3-2. 오브젝트 문맥 계층

특정 오브젝트나 기능이 어떤 상황에서 어떤 소리를 요청할지 캡슐화한다.

- `CharacterAudio`
- `WeaponAudioEmitter`
- `UIAudioEmitter`
- `GameplayAudioPresenter`

### 3-3. 중앙 서비스 계층

요청을 받아 실제 재생 채널을 확보하고 재생 정책을 적용한다.

- `IAudioService`
- `AudioManager`
- `BgmController`
- `AudioSourcePool`
- `AttachedAudioRegistry`

### 3-4. Unity 실행 계층

실제 Unity 런타임 객체다.

- `AudioSource`
- `AudioMixer`
- `AudioMixerGroup`
- `Transform`
- `AudioClip`

### 3-5. Feature 매핑 계층

특정 Feature가 어떤 공용 오디오 자산을 어떤 도메인 이벤트에 연결할지 정의한다.

- `GameplayAudioMap`
- `UIAudioMap`
- `WeaponAudioMap`

이 계층은 공용 사운드 자산을 재사용하되, 그 자산을 어떤 문맥에서 쓸지는 Feature가 결정하도록 만드는 완충 지점이다.

## 4. 폴더 및 네임스페이스 배치

Hybrid 구조 기준으로 다음처럼 배치한다.

```text
Assets/
  _Core/
    Runtime/
      Audio/
        IAudioService.cs
        AudioManager.cs
        AudioPlaybackHandle.cs
        AudioPlaybackContext.cs
        AudioPlaybackData.cs
        AudioSourcePool.cs
        BgmController.cs
        AttachedAudioRegistry.cs
        AudioCategory.cs

  _Shared/
    Audio/
      Definitions/
        BGM/
        SFX/
        UI/
        Voice/
      Mixers/
      RuntimeConfig/
        AudioBusConfiguration.asset
      Clips/

  _Features/
    Gameplay/
      Gameplay_Audio/
        Runtime/
          GameplayAudioPresenter.cs
          GameplayAudioMap.cs
          GameplayAudioCue.cs
      Gameplay_Host/
        Runtime/
          GameplaySceneHost.cs
          GameplayInputHost.cs

    UI/
      UI_Audio/
        Runtime/
          UIAudioEmitter.cs

    Weapons/
      Weapon_Audio/
        Runtime/
          WeaponAudioEmitter.cs
```

권장 네임스페이스는 다음과 같다.

- `_Core/Runtime/Audio` -> `Game.Core.Audio`
- `_Shared/Audio` -> `Game.Shared.Audio`
- `_Features/Gameplay/Gameplay_Audio` -> `Game.Feature.Gameplay.Audio`

배치 기준은 다음과 같다.

- 오디오 재생 인프라와 공용 계약은 `Core`
- 공용 오디오 자산과 믹서 설정은 `Shared`
- 특정 게임 규칙과 연결되는 오디오 매핑과 프레젠터는 `Feature`

## 5. 핵심 책임

### 5-1. `AudioDefinition`

역할:
이 사운드를 어떤 규칙으로 재생할지 정의하는 데이터 자산

소유 정보:

- 카테고리
- 기본 볼륨
- 피치 범위
- 루프 여부
- 2D/3D 여부
- spatial blend
- min/max distance
- priority
- retrigger cooldown
- max concurrent count

이 타입은 `AudioClip` 하나 또는 여러 개를 감쌀 수 있지만, 상위 로직은 그 내부 클립 구조를 몰라야 한다.

### 5-2. `SingleAudioDefinition`

역할:
클립 1개를 1개의 재생 규칙으로 다루는 기본형 정의

용도:

- UI 버튼 클릭
- 단일 공격음
- 단일 폭발음

### 5-3. `RandomAudioDefinition`

역할:
여러 클립 또는 여러 하위 정의 중 하나를 선택해 재생하는 변형형 정의

용도:

- 발소리 변주
- 피격음 변주
- 좀비 신음소리 변주

초기 버전은 가중치 랜덤만 지원하면 충분하다.

### 5-4. `AudioPlaybackData`

역할:
실제 재생 직전에 확정된 실행 데이터

구분:

- `AudioDefinition` = 설계도
- `AudioPlaybackData` = 이번 재생의 최종 결과물

권장 필드는 다음과 같다.

```csharp
public readonly struct AudioPlaybackData
{
    public AudioClip Clip { get; }
    public AudioCategory Category { get; }
    public float Volume { get; }
    public float Pitch { get; }
    public bool Loop { get; }
    public float SpatialBlend { get; }
    public float MinDistance { get; }
    public float MaxDistance { get; }
    public int Priority { get; }
}
```

### 5-5. `AudioPlaybackContext`

역할:
재생 시점 오버라이드와 문맥 정보를 담는 요청 컨텍스트

권장 필드는 다음과 같다.

```csharp
public readonly struct AudioPlaybackContext
{
    public float VolumeMultiplier { get; init; }
    public float PitchMultiplier { get; init; }
    public Vector3 Position { get; init; }
    public Transform FollowTarget { get; init; }
    public int? OwnerEntityId { get; init; }
    public string DebugTag { get; init; }
}
```

중요한 점은, 이 컨텍스트는 재생 오버라이드를 위한 것이지 사운드 정의 자체를 수정하는 것이 아니다.

### 5-6. `IAudioService`

역할:
상위 시스템이 의존하는 오디오 재생 인터페이스

권장 계약은 다음과 같다.

```csharp
public interface IAudioService
{
    AudioPlaybackHandle Play2D(AudioDefinition definition, in AudioPlaybackContext context = default);
    AudioPlaybackHandle Play3D(AudioDefinition definition, Vector3 position, in AudioPlaybackContext context = default);
    AudioPlaybackHandle PlayAttached(AudioDefinition definition, Transform target, in AudioPlaybackContext context = default);
    AudioPlaybackHandle PlayBgm(AudioDefinition definition, float fadeInSeconds = 0f);
    void Stop(AudioPlaybackHandle handle, float fadeOutSeconds = 0f);
    void StopBgm(float fadeOutSeconds = 0f);
}
```

Feature 쪽은 이 인터페이스만 알고 `AudioManager` 구현 세부를 몰라야 한다.

### 5-7. `AudioPlaybackHandle`

역할:
지속 사운드나 BGM처럼 나중에 제어가 필요한 재생 인스턴스를 다루는 핸들

필수 기능:

- `Stop`
- `Pause`
- `Resume`
- `SetVolume`
- `IsValid`

one-shot에서도 일관성을 위해 핸들을 반환할 수 있지만, 실제 사용은 attached loop와 BGM에서 중요하다.

### 5-8. `AudioManager`

역할:
오디오 시스템의 중앙 구현체

주요 책임:

- 요청 수신
- `AudioDefinition` 해석
- `AudioPlaybackData` 생성
- 카테고리별 라우팅
- 풀 또는 전용 채널 확보
- 실제 `AudioSource` 재생
- 동시 재생 제한과 쿨타임 적용

이 객체는 중앙 진입점이며, 개별 Feature가 직접 `AudioSource`를 만들지 않게 막는 핵심 경계다.

### 5-9. `AudioSourcePool`

역할:
SFX/UI/Voice/Ambience용 `AudioSource` 재사용 인프라

동작:

1. 초기 개수를 미리 생성한다.
2. 요청 시 비어 있는 소스를 빌린다.
3. 재생 종료 시 다시 반환한다.
4. 부족할 경우 정책에 따라 확장하거나 낮은 우선순위 소스를 스틸한다.

초기 정책은 다음으로 단순화한다.

- SFX/UI one-shot은 풀 사용
- loop attached는 풀에서 빌려 장기 점유
- BGM은 풀을 쓰지 않고 전용 채널 사용

### 5-10. `BgmController`

역할:
배경음 전용 재생/정지/전환 모듈

초기 권장 구성:

- `AudioSource` 2개
- A/B 크로스페이드
- 루프 지원
- `DontDestroyOnLoad` 가능

즉, BGM은 일반 효과음과 다른 수명과 정책을 가지므로 별도 모듈이 맞다.

### 5-11. `AttachedAudioRegistry`

역할:
대상 `Transform`에 붙어 따라다니는 재생 인스턴스를 관리하는 레지스트리

용도:

- 엔진 소리
- 화염 루프
- 전기장
- 채널링 사운드

초기 구현은 다음 규칙이면 충분하다.

- 재생 중인 풀 오브젝트를 대상 `Transform`의 자식으로 붙인다.
- 정지 시 풀 루트로 되돌린다.

## 6. 데이터 자산 설계

### 6-1. `AudioCategory`

초기 카테고리는 다음으로 고정한다.

- `Master`
- `BGM`
- `SFX`
- `UI`
- `Voice`
- `Ambience`

카테고리의 목적은 단순 분류가 아니다.

- 옵션 메뉴 볼륨 제어
- `AudioMixerGroup` 라우팅
- 우선순위 정책 차등 적용
- 채널별 동시 재생 정책 분리

### 6-2. `AudioBusConfiguration`

역할:
카테고리와 실제 `AudioMixerGroup` 및 exposed parameter 이름을 연결하는 공용 설정 자산

권장 내용:

- `AudioMixer` 참조
- `BGM`, `SFX`, `UI`, `Voice`, `Ambience` 그룹 참조
- exposed volume parameter 이름
- 초기 볼륨 값

이 자산이 있으면 `AudioDefinition`은 카테고리만 들고 있어도 중앙 라우팅이 가능하다.

### 6-3. `GameplayAudioMap`

역할:
Gameplay 도메인 이벤트와 실제 공용 오디오 정의를 연결하는 Feature 전용 매핑 자산

예시 필드:

- `playerMove`
- `playerBlocked`
- `boxSlide`
- `boxDestroyed`
- `throwStart`
- `projectileHit`
- `entityDeath`

중요한 분리:

- `AudioDefinition`은 Shared
- `Gameplay에서 무엇에 어떤 사운드를 쓸지`는 Feature

## 7. 오브젝트 문맥 계층

### 7-1. Emitter의 역할

`Emitter`는 중앙 서비스가 아니다.

`Emitter`의 본질은 다음과 같다.

> 특정 오브젝트 관점에서 오디오 요청을 캡슐화하는 얇은 어댑터

예를 들면:

- `WeaponAudioEmitter.PlayFire()`
- `UIAudioEmitter.PlayClick()`
- `CharacterAudio.PlayFootstep()`

다른 시스템은 정의 자산, 볼륨, 위치 전달 방식을 몰라도 된다.

### 7-2. Emitter 설계 규칙

- `Emitter`는 `AudioSource`를 직접 만들지 않는다.
- `Emitter`는 중앙 `IAudioService`로 요청만 전달한다.
- `Emitter`는 자기 오브젝트 문맥의 사운드만 소유한다.
- 범용 거대 `AudioEmitterBase`를 먼저 만들지 않는다.
- 중복이 실제로 생기기 전까지는 Feature별 얇은 컴포넌트를 유지한다.

이 규칙을 지키면 문맥 응집도는 높고, 중앙 재생 인프라와의 결합은 낮아진다.

## 8. 이 프로젝트의 Gameplay 통합 방식

현재 프로젝트는 Tick 시뮬레이션과 View Presenter 구조를 사용한다.

따라서 Gameplay 오디오 통합은 아래 정책을 따른다.

### 8-1. 금지 규칙

다음 계층에서는 오디오를 직접 재생하지 않는다.

- `PlayerLogic`
- `ProjectileLogic`
- `TickPipeline`
- `MovementCommitter`
- `AttackCommitter`
- `CleanupProcessor`
- `WorldState`

### 8-2. 허용 경로

Gameplay 오디오는 다음 두 경로로만 발생시킨다.

1. 입력/버튼처럼 즉시성 높은 View 이벤트
2. `TickResult`를 해석한 Presenter 이벤트

### 8-3. 권장 구성

Gameplay에는 별도 `GameplayAudioPresenter`를 둔다.

```text
GameplayInputHost
    -> TickRunner
        -> TickResult
            -> GameplayTickViewPresenter
            -> GameplayAudioPresenter
                -> IAudioService
                    -> AudioManager
```

이 Presenter의 역할은 다음과 같다.

- `TickResult` 또는 `PresentationCue`를 읽는다.
- 필요한 월드 위치를 계산한다.
- `GameplayAudioMap`에서 적절한 `AudioDefinition`을 찾는다.
- `IAudioService`에 재생 요청을 보낸다.

즉, Gameplay 오디오는 View와 같은 프레젠테이션 계층의 일부로 다뤄야 한다.

### 8-4. `TickResult.EventLog`를 직접 쓰지 않는 이유

현재 `EventLog`는 문자열 기반 디버그 로그다.

이것을 오디오 트리거 계약으로 쓰면 다음 문제가 생긴다.

- 문자열 포맷 변경이 곧 오디오 회귀가 된다.
- 디버그 관심사와 런타임 프레젠테이션 관심사가 섞인다.
- 타이핑 안전성이 없다.

따라서 Gameplay 오디오용 신호는 다음 둘 중 하나를 택한다.

- `TickResult`에 `PresentationCue`를 추가한다.
- 또는 `GameplayAudioPresenter`가 phase result/state delta를 직접 해석한다.

초기 버전 권장안은 다음이다.

> 첫 구현은 `GameplayAudioPresenter`가 `TickResult`의 상태 변화와 phase result를 해석한다.  
> 이후 VFX/SFX/UI 반응이 늘어나면 공용 `PresentationCue`로 승격한다.

## 9. 실제 동작 흐름

### 9-1. UI 버튼 클릭

```text
Button.onClick
    -> UIAudioEmitter.PlayClick()
        -> IAudioService.Play2D(uiClickDefinition)
            -> AudioManager
                -> AudioSourcePool
                    -> AudioSource.Play()
```

이 경우는 원인과 결과가 명확하므로 직접 호출이 맞다.

### 9-2. Gameplay 이동/충돌/공격

```text
GameplayInputHost.RunSingleTick()
    -> TickRunner.RunNextTick()
        -> TickResult
    -> GameplayTickViewPresenter.Present(result)
    -> GameplayAudioPresenter.Present(result)
        -> GameplayAudioMap.Resolve(...)
        -> IAudioService.Play3D(...)
            -> AudioManager
```

이 경우는 시뮬레이션 결과를 프레젠테이션 계층이 해석해 사운드를 낸다.

### 9-3. 지속 부착 사운드

```text
WeaponChargeController.StartCharge()
    -> WeaponAudioEmitter.PlayChargeLoop()
        -> handle = IAudioService.PlayAttached(chargeLoopDefinition, transform)

WeaponChargeController.CancelCharge()
    -> handle.Stop()
```

### 9-4. BGM 전환

```text
StageFlowController.EnterBattle()
    -> IAudioService.PlayBgm(battleBgmDefinition, fadeInSeconds: 1.0f)
        -> BgmController.Crossfade()
```

## 10. 중앙 서비스 내부 모듈 분해

`AudioManager`는 하나의 진입점이지만, 내부 책임은 다음처럼 나눈다.

### 10-1. Request Validation

- null 정의 방어
- 카테고리/정의 타입 확인
- 쿨타임 체크
- 동시 재생 제한 체크

### 10-2. Definition Resolution

- `AudioDefinition` -> `AudioPlaybackData`
- 랜덤 클립 선택
- pitch/volume 랜덤 반영

### 10-3. Routing

- `AudioCategory` -> `AudioMixerGroup`
- `BGM`이면 `BgmController`
- 일반 one-shot이면 `AudioSourcePool`
- attached loop면 `AttachedAudioRegistry`

### 10-4. Playback Ownership

- 어떤 핸들이 어떤 `AudioSource`를 점유 중인지 추적
- 정지 시 pool 반환
- 파괴된 target에 붙은 attached 사운드 정리

## 11. 조립 방식

현재 저장소에는 전역 DI 컨테이너가 없으므로, 초기 버전 조립은 Scene Host 방식이 가장 맞다.

권장 조립은 다음과 같다.

### 11-1. `AudioManager`

- 별도 `GameObject`에 배치
- `DontDestroyOnLoad` 사용 가능
- `AudioBusConfiguration`과 pool 설정을 serialize

### 11-2. `SampleSceneInstaller`

현재 `SampleSceneInstaller`가 `GameplaySceneHost`를 조립하므로, 초기 버전에서는 동일한 위치에서 `AudioManager`도 확보한다.

예시 흐름:

```text
SampleSceneInstaller.Awake()
    -> Ensure AudioManager
    -> Ensure GameplaySceneHost
    -> GameplaySceneHost.Initialize(configuration with audio dependencies)
```

### 11-3. `GameplaySceneHostConfiguration`

초기 오디오 통합을 위해 아래 항목 추가를 권장한다.

- `IAudioService AudioService`
- `GameplayAudioMap GameplayAudioMap`

이렇게 하면 Gameplay 쪽은 구체적인 `AudioManager`에 의존하지 않고 계약만 받는다.

## 12. 우선 채택 정책

초기 구현은 다음 정책으로 제한한다.

### 12-1. 포함

- `SingleAudioDefinition`
- `RandomAudioDefinition`
- `IAudioService`
- `AudioManager`
- `AudioSourcePool`
- `BgmController`
- `UIAudioEmitter`
- `GameplayAudioPresenter`
- `GameplayAudioMap`

### 12-2. 후순위

- 가중치 랜덤 고도화
- 우선순위 기반 source stealing
- 스냅샷 전환
- 옵션 저장/로드 영속화
- Voice ducking
- Timeline 동기화

즉, v1은 구조를 먼저 고정하고 기능은 최소 필수만 넣는다.

## 13. 테스트 전략

오디오는 Unity 런타임 의존성이 있지만, 계약과 정책은 상당 부분 테스트 가능하다.

### 13-1. EditMode 테스트

대상:

- `AudioDefinition` -> `AudioPlaybackData` 변환
- 쿨타임/동시 재생 제한 정책
- `GameplayAudioPresenter`의 cue 매핑
- `GameplayAudioMap` 누락 검증

### 13-2. PlayMode 테스트

대상:

- `AudioManager`가 실제로 pool을 재사용하는지
- attached 사운드가 target을 따라가는지
- BGM 교체 시 두 소스가 정상 크로스페이드되는지
- Gameplay tick 후 예상 사운드 요청이 발생하는지

### 13-3. 구조 테스트

강제할 규칙:

- `Gameplay_Loop`이 `Game.Core.Audio`를 참조하지 않아야 한다.
- `AudioDefinition` 자산이 Feature namespace를 참조하지 않아야 한다.
- `TickPipeline`에서 `Play`, `AudioSource`, `IAudioService`를 호출하지 않아야 한다.

## 14. 확장 경로

이 설계 위에서 다음 기능을 무리 없이 확장할 수 있다.

- 가중치 랜덤
- 동일 사운드 재트리거 쿨타임
- 카테고리별 최대 동시 재생 수
- 거리 기반 감쇠 조정
- BGM crossfade와 snapshot 전환
- 옵션 메뉴 볼륨 저장/로드
- Feature별 오디오 프로파일 전환

이 기능들이 쉬운 이유는 처음부터 데이터, 문맥, 재생 인프라가 분리되어 있기 때문이다.

## 15. 최종 요약

이 프로젝트에서 채택할 오디오 구조는 다음과 같다.

```text
[Feature 입력/프레젠테이션 계층]
UI Button / GameplayAudioPresenter / WeaponAudioEmitter

        ↓

[오브젝트 문맥 계층]
CharacterAudio / UIAudioEmitter / WeaponAudioEmitter

        ↓

[중앙 계약 계층]
IAudioService

        ↓

[중앙 구현 계층]
AudioManager
 ├─ Definition Resolution
 ├─ AudioSourcePool
 ├─ AttachedAudioRegistry
 └─ BgmController

        ↓

[데이터 자산 계층]
AudioDefinition
 ├─ SingleAudioDefinition
 ├─ RandomAudioDefinition
 └─ AudioBusConfiguration

        ↓

[Unity 실행 계층]
AudioSource / AudioMixer / AudioMixerGroup / AudioClip
```

이 구조에서 가장 중요한 설계 판단은 세 가지다.

- 사운드 정의는 데이터 자산으로 분리한다.
- 오브젝트별 오디오 요청은 문맥 컴포넌트로 캡슐화한다.
- Gameplay Tick 시뮬레이션은 오디오를 모르고, Presenter 계층만 결과에 반응한다.

이 세 가지를 지키면, 지금 필요한 최소 오디오 기능부터 이후 확장까지 구조적으로 안전하게 가져갈 수 있다.
