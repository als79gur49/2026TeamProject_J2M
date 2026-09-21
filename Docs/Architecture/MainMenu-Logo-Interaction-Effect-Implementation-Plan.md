# MainMenu Logo Interaction Effect Implementation Plan

## 1. 문서 상태와 목적

- 상태: 구현 전 승인 가능한 계획안
- 범위: `MainMenuScreen`의 Logo와 Logo 뒤쪽 장식 효과
- 목표: 포인터 hover, 키보드·게임패드 focus, 수락된 메뉴 명령에 대해 Logo가 일관된 시각 피드백을 제공한다.
- 비목표: MainMenu 명령 의미, 화면 전환 정책, 저장 데이터, gameplay 상태를 변경하지 않는다.

이 문서는 [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)와
[UI-Authoring-and-Navigation-Guide.md](./UI-Authoring-and-Navigation-Guide.md)를 따르는 범위 한정 구현 계획이다.
두 문서와 충돌하면 canonical UI architecture와 authoring/navigation 계약이 우선한다.

## 2. 결정 요약

1차 구현은 다음 하이브리드 구성을 사용한다.

| 책임 | 구현 수단 | 결정 |
| --- | --- | --- |
| 효과 수명주기와 입력 반응 | 코드 기반 DOTween Sequence | 채택 |
| V에서 시작해 오른쪽으로 흐르는 광택 | AllIn1Sprite UI Shine | 채택 |
| 평상시 배경 호흡 | 기존 `ParticleFX_Glow`의 root scale/alpha tween | 채택 |
| 수락된 명령의 짧은 충격 | Logo parent의 deterministic transform tween | 채택 |
| 수락된 명령의 불꽃 | 별도 UI ParticleSystem의 수동 `Emit` | 채택 |
| 전체 상태 제어 | Animator Controller | 1차 구현에서 제외 |
| Shader 시간 구동 | global unscaled-time updater | 제외 |

Animator는 디자이너가 clip timeline을 직접 편집해야 하는 요구가 생기면 후속 선택지로 재검토한다.
현재 요구는 입력 상태, modal 차단, 명령 수락 결과, shader property를 함께 조정해야 하므로 코드 기반 Sequence가 더 좁고 명시적이다.

## 3. 사용자 경험 계약

### 3.1 기본 상태

- Logo 본체는 계속 흔들거나 크게 호흡하지 않는다.
- Logo 뒤의 Glow만 `2.8~4.0`초 주기로 약하게 호흡한다.
- Screen Space Overlay UI이므로 URP Bloom에 의존하지 않는다.
- 낮은 밝기의 additive/faux glow를 사용하며 본문 가독성을 침범하지 않는다.

초기 권장값:

| 속성 | 값 |
| --- | --- |
| cycle | `3.2s`, yoyo loop |
| scale | `1.000 -> 1.035` |
| alpha | `0.18 -> 0.26` |
| ease | `InOutSine` |
| time source | unscaled |

### 3.2 Hover와 navigation focus

포인터가 MainMenu 명령에 진입하거나 키보드·게임패드 focus가 다른 명령으로 이동하면 다음 효과를 한 번 재생한다.

1. V 근처에서 얇은 금백색 광택이 점등된다.
2. 광택이 Logo 오른쪽 끝으로 이동한다.
3. 광택 세기가 0으로 돌아간다.

초기 권장값:

| 속성 | 값 |
| --- | --- |
| duration | `0.38s` (`0.32~0.45s` 조정 범위) |
| shine location | `0.18 -> 0.93` |
| shine width | `0.10~0.12` |
| shine rotation | 약 `0.785rad` |
| ease | `OutCubic` |
| same-target debounce | `0.20s` |

포인터 hover와 navigation focus는 동등한 반응을 제공한다. 포인터 exit가 살아 있는 navigation focus를 지우면 안 된다.

### 3.3 수락된 명령

버튼이 눌렸다는 사실이 아니라 명령이 실제 UI flow에서 수락됐을 때만 impact를 재생한다.

효과는 다음 조합이다.

- Logo 위치 `x/y` 최대 `2~3px`의 짧은 deterministic micro-quake
- 최대 `1.022` scale 후 원상 복귀
- 금색 spark `6~10`개 수동 방출
- 총 길이 약 `0.12~0.22s`

권장 transform key:

| 시점 | anchored position | scale |
| --- | --- | --- |
| `0ms` | `(0, 0)` | `1.000` |
| `25ms` | `(+2, 0)` | `1.022` |
| `50ms` | `(-3, +1)` | 유지 |
| `80ms` | `(+2, -1)` | 복귀 시작 |
| `115ms` | `(0, 0)` | `1.000` |

무작위 `DOShakeAnchorPos`나 지속 glitch는 사용하지 않는다. 같은 입력은 같은 모션을 만들어야 하며 테스트와 아트 튜닝 결과가 재현 가능해야 한다.

### 3.4 차단 상태

다음 상태에서는 새로운 focus/impact 효과를 시작하지 않는다.

- Confirm popup이 입력 우선권을 가진 상태
- Settings modal overlay가 열린 상태
- Save Slot section 전환으로 MainMenu command domain이 비활성화된 상태
- Iris 또는 gameplay launch source-close가 진행 중인 상태
- 화면 비활성화·파괴 또는 application focus 상실로 입력을 받을 수 없는 상태

차단 진입 시 진행 중인 one-shot tween을 kill하고 transient property를 기본값으로 복구한다. Idle Glow를 유지할지는 화면 visibility와 active 상태에 맞춰 결정하며, 비활성 화면에서는 정지한다.

## 4. 상호작용 의미

Logo 자체는 독립된 명령이 아니다.

- 기존 `Logo` Image의 `raycastTarget=false`를 유지한다.
- Logo를 `Button` 또는 `Selectable`로 바꾸지 않는다.
- 투명한 Logo 전용 hit target을 추가하지 않는다.
- MainMenu 명령의 hover, selection, accepted 결과가 Logo 효과를 구동한다.

이 결정은 의미 없는 clickable surface를 만들지 않고 pointer와 keyboard parity를 보존한다.

## 5. Prefab authoring 계획

대상은 `Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab`이다.

권장 hierarchy:

```text
ContentHost
└─ LogoEffectRoot
   ├─ ParticleFX_Glow
   ├─ LogoMotionRoot
   │  └─ Logo
   └─ LogoSparkBurst
```

authoring 계약:

- 기존 오브젝트 이름 `Logo`를 유지한다.
- 기존 `Image` component와 `Assets/3DM/Sprite/tittl3e.png` 참조를 유지한다.
- `Logo.raycastTarget=false`를 유지한다.
- Logo의 현재 authored anchored position을 직접 tween하지 않는다.
- 원점 transform인 `LogoMotionRoot`가 impact 위치와 scale을 소유한다.
- 기존 `ParticleFX_Glow`는 Logo 뒤의 sibling order를 유지한다.
- `LogoSparkBurst`는 `Loop=false`, `PlayOnAwake=false`인 별도 UI particle로 authoring한다.
- 새 material, particle asset과 Unity가 생성한 `.meta`는 함께 변경한다.

고정된 player-visible hierarchy이므로 prefab에 authoring한다. 런타임은 누락된 시각 hierarchy를 생성하거나 자동 복구하지 않고 serialized reference를 검증한다.

## 6. Runtime 책임 분리

### 6.1 `MainMenuLogoEffectView`

위치: `Assets/_Features/UI/UI_Screens/Runtime/MainMenuLogoEffectView.cs`

이 component는 시각 효과만 소유한다.

권장 serialized reference:

```csharp
[SerializeField] private Image _logoImage;
[SerializeField] private RectTransform _impactRoot;
[SerializeField] private RectTransform _glowRoot;
[SerializeField] private CanvasGroup _glowCanvasGroup;
[SerializeField] private ParticleSystem _acceptedBurst;
[SerializeField] private Material _allIn1Template;
```

권장 public surface:

```csharp
public void SetInteractionBlocked(bool blocked);
public void PlayFocusShine(MainMenuLogoFocus focus);
public void PlayAccepted(MainMenuLogoImpactKind kind);
public void RestoreImmediate();
```

규칙:

- View는 메뉴 command를 실행하지 않는다.
- View는 popup, save, settings 또는 scene transition 정책을 판단하지 않는다.
- effect별 tween handle을 분리한다: `_idleTween`, `_shineTween`, `_impactTween`.
- 서로 다른 target/property를 사용해 idle, shine, impact가 불필요하게 kill되지 않게 한다.
- 모든 tween은 `.SetUpdate(true)`와 `.SetLink(gameObject, LinkBehaviour.KillOnDestroy)`를 적용한다.
- `OnDisable`은 one-shot tween을 kill하고 position, scale, shader 값을 복구한다.
- `OnDestroy`는 원본 material을 복원한 뒤 runtime clone을 파괴한다.

### 6.2 `MainMenuCommandFeedbackRelay`

위치: `Assets/_Features/UI/UI_Screens/Runtime/MainMenuCommandFeedbackRelay.cs`

각 command button의 기존 `UiHoverScaleEffect`를 대체하지 않고 semantic focus 변화를 MainMenu screen에 알린다.

권장 역할:

- pointer enter/exit 관측
- `IUiSelectionFeedback`을 통한 navigation focus/submit 관측
- 기존 `UiHoverScaleEffect`에 navigation/submit feedback 위임
- command ID와 focus source를 `MainMenuScreenView`에 전달

`UiSelectableButtonSlot.SelectionFeedback`가 Relay를 가리키는 경우 Relay는 기존 scale feedback을 반드시 위임해야 한다. 그렇지 않으면 keyboard focus에서 기존 버튼 피드백이 사라진다.

권장 semantic event:

```csharp
public event Action<MainMenuCommandFocusChanged> CommandFocusChanged;
```

focus 상태 계산은 pointer와 navigation을 별도로 추적한다.

```text
blocked -> none
pointer hovered command -> pointer command
else visible navigation focus -> selected command
else -> none
```

### 6.3 `MainMenuLogoFeedbackController`

위치: `Assets/_Features/UI/UI_Composition/Runtime/MainMenuLogoFeedbackController.cs`

이 controller는 UI 상태를 Logo 표현 명령으로 변환한다.

책임:

- `MainMenuScreenView.CommandFocusChanged` 구독
- popup/settings/save-slot/transition 상태로 block 여부 계산
- accepted lifecycle만 `PlayAccepted`로 전달
- 화면 교체·dispose 때 모든 구독 해제

`MainMenuUiFlowInstaller`는 View와 Controller를 조립하고 수명주기를 소유한다. Logo Effect View가 flow service를 직접 찾거나 static event를 구독하면 안 된다.

## 7. Accepted event 연결 기준

raw `CommandRequested`는 intent이며 성공 결과가 아니므로 impact의 최종 근거로 사용하지 않는다.

| 메뉴 경로 | accepted 기준 | 실패·거절 시 동작 |
| --- | --- | --- |
| Start | Save Slots section 전환이 실제 적용됨 | impact 없음 |
| Settings | Settings overlay가 실제로 열림 | impact 없음 |
| Quit | Quit Confirm popup이 실제로 열림 | impact 없음 |
| Stage launch | gameplay entry source-close 요청이 수락됨 | impact 없음 |
| Prepare Participant 등 추가 명령 | 해당 flow의 확인 popup 또는 accepted lifecycle | impact 없음 |

한 intent에서 popup-open과 후속 transition이 모두 발생하더라도 같은 semantic action의 impact는 정확히 한 번이어야 한다. Controller가 pending accepted token 또는 bounded suppression state를 소유해 중복을 막는다.

## 8. AllIn1Sprite material 계약

새 serialized template material을 권장한다.

예상 위치:

```text
Assets/_Features/UI/UI_Screens/Materials/LogoShine_AllIn1.mat
```

material 계약:

- shader: `AllIn1SpriteShader/AllIn1SpriteShaderUiMask`
- keyword: `SHINE_ON`
- 평상시 `_ShineGlow=0`
- 런타임에 template을 한 번만 clone
- Logo Image에는 clone을 할당
- hover마다 material을 생성하지 않음
- hover마다 shader keyword를 켜고 끄지 않음
- DOTween으로 `_ShineLocation`과 `_ShineGlow`만 명시적으로 변경

serialized template은 Player build에서 shader와 variant가 참조되도록 한다. `Shader.Find`만으로 material을 구성하지 않는다.

AllIn1Sprite의 역할은 Shine에 한정한다.

- `GLOW_ON`은 외곽 halo의 대체 수단으로 간주하지 않는다.
- `SHAKEUV_ON`은 deterministic click impact에 사용하지 않는다.
- glitch, chromatic aberration, rainbow 계열 keyword는 Logo 정체성과 가독성을 해치므로 사용하지 않는다.
- spark는 shader가 아니라 별도 UI ParticleSystem이 소유한다.

uGUI Image에는 `MaterialPropertyBlock` 경로가 없으므로 instance별 material clone을 허용한다. 이 선택은 Logo 하나에 한정하며 material/draw-call 증가는 구현 검증에서 측정한다.

## 9. Particle 계약

기존 `ParticleFX_Glow`를 click 때 `Stop/Clear/Play`하지 않는다. 지속 장식과 one-shot burst는 서로 다른 particle system을 사용한다.

`LogoSparkBurst` 초기 권장값:

| 속성 | 값 |
| --- | --- |
| loop | false |
| play on awake | false |
| simulation | unscaled |
| regular emission | 0 |
| manual emit count | `6~10` |
| lifetime | `0.25~0.45s` |
| max particles | 약 `16` |
| color | gold/white에서 transparent로 |
| raycast | disabled |

click마다 particle GameObject 또는 material을 instantiate하지 않는다.

## 10. Tween 충돌과 수명주기

### 10.1 재진입

- 새 focus가 오면 기존 `_shineTween`만 kill한다.
- `_ShineLocation`, `_ShineGlow`를 초기화하고 새 shine을 처음부터 재생한다.
- 같은 command의 빠른 중복 focus는 debounce한다.
- 새 accepted impact가 오면 기존 `_impactTween`을 기본 transform으로 정리한 뒤 재생한다.

### 10.2 비활성화와 파괴

`RestoreImmediate()`는 다음 상태를 보장한다.

```text
LogoMotionRoot anchoredPosition = authored base position
LogoMotionRoot localScale       = authored base scale
_ShineLocation                  = initial value
_ShineGlow                      = 0
particle burst                  = stopped/cleared when screen closes
```

원본 material reference와 authored transform 값을 초기화 시 저장한다. `OnDisable`과 `OnDestroy`를 여러 번 호출해도 안전해야 한다.

## 11. 성능 예산

목표:

- idle frame managed allocation: `0 B/frame`
- Logo별 runtime material clone: 최대 1개
- tween: idle 최대 1개, shine 최대 1개, impact 최대 1개
- click burst: 사전 authoring된 particle 재사용
- 예상 draw call 증가: material 분리와 burst에 따른 제한적 `+1~2`; 실제 Editor/Player에서 확인

기존 UI particle은 mesh를 갱신하므로 입자 수와 overdraw를 작게 유지한다. 지속 glow와 burst가 동시에 화면 전체를 덮는 큰 quad를 만들지 않는다.

## 12. 구현 순서

1. Logo prefab contract와 현재 MainMenu command/navigation lifecycle에 characterization test를 추가한다.
2. `LogoEffectRoot`, `LogoMotionRoot`, `LogoSparkBurst`를 prefab에 authoring한다.
3. `LogoShine_AllIn1.mat` template을 추가하고 Logo Image에 runtime clone 경로를 연결한다.
4. `MainMenuLogoEffectView`에 idle, shine, impact의 독립 tween과 lifecycle cleanup을 구현한다.
5. `MainMenuCommandFeedbackRelay`로 pointer와 navigation focus를 하나의 semantic event로 노출한다.
6. `MainMenuLogoFeedbackController`를 composition에 연결하고 block/accepted 정책을 구현한다.
7. EditMode contract와 lifecycle tests를 통과시킨다.
8. Editor에서 pointer/keyboard/gamepad parity와 실제 시각 강도를 확인한다.
9. Player build에서 AllIn1Sprite shader variant, overlay rendering, GC와 draw call을 확인한다.

## 13. 자동 검증 계획

UI 변경이므로 기본 lane은 다음과 같다.

```bash
./run_tests.sh ui
```

추가할 focused 검증:

- prefab의 `Logo` 이름, Image, sprite, `raycastTarget=false` 유지
- Logo effect serialized reference가 모두 유효함
- AllIn1 template material과 `SHINE_ON` 계약 유지
- pointer hover와 navigation focus가 각각 shine을 발생시킴
- pointer exit가 남아 있는 navigation focus를 제거하지 않음
- focus 비공개 상태에서는 keyboard highlight/shine을 잘못 노출하지 않음
- blocked 상태에서는 새 shine/impact가 시작되지 않음
- raw command request가 거절되면 impact가 발생하지 않음
- accepted semantic action당 impact가 정확히 한 번 발생함
- disable/enable 뒤 transform과 shader property가 기본값으로 복구됨
- runtime material clone이 재활성화마다 누적되지 않음
- 기존 `UiHoverScaleEffect`의 pointer/navigation/submit 피드백이 유지됨

Prefab, material, particle 변경은 자동 테스트만으로 시각 결과를 증명하지 않는다.

## 14. 수동·Player 검증 체크리스트

- [ ] 1920×1080에서 Logo가 기존 위치와 크기를 유지한다.
- [ ] 지원하는 다른 aspect ratio에서도 Glow와 Spark가 Logo를 벗어나 잘리지 않는다.
- [ ] mouse hover와 keyboard/gamepad focus의 Logo 반응이 동등하다.
- [ ] 빠르게 버튼 사이를 이동해도 shine이 멈춘 밝은 상태로 남지 않는다.
- [ ] click을 연타해도 transform drift나 scale 누적이 없다.
- [ ] Settings/Confirm/Save Slots/launch 전환 중 하위 메뉴 Logo가 반응하지 않는다.
- [ ] V에서 시작하는 shine이 글자 가독성을 해치지 않는다.
- [ ] URP Bloom 없이도 overlay UI에서 glow가 의도대로 보인다.
- [ ] Windows Player에서 AllIn1Sprite shader가 pink/missing 또는 keyword 누락 상태가 아니다.
- [ ] Profiler에서 idle managed allocation이 없고 particle overdraw가 제한적이다.

## 15. 후속 선택지와 비목표

1차 범위에 포함하지 않는다.

- V 전용 분리 sprite 또는 정밀 mask 제작
- Logo 자체 클릭 command 추가
- Animator Controller와 `.anim` asset 도입
- 지속 glitch, chromatic aberration, rainbow, 큰 scale pulse
- gameplay 상태에 따른 Logo 색상 변화
- 기존 MainMenu background/camera transition 재설계

단일 shine band가 V에서 출발하는 인상을 충분히 만들지 못할 때만 V overlay/mask를 후속 작업으로 분리한다. Animator 도입도 동일하게 artist-authored timeline 편집 필요가 확인된 뒤 별도 intent로 검토한다.

## 16. 완료 조건

다음을 모두 만족하면 이 계획의 구현이 완료된 것으로 본다.

1. Logo의 기존 asset·이름·layout·raycast 계약이 보존된다.
2. pointer와 navigation focus가 같은 semantic shine을 만든다.
3. 실제 수락된 명령만 deterministic impact와 spark를 정확히 한 번 만든다.
4. modal과 transition block이 하위 Logo 반응을 막는다.
5. tween, material clone, particle이 disable/destroy에서 누수 없이 정리된다.
6. `./run_tests.sh ui` 결과와 수동 Editor/Player 검증 결과가 기록된다.
7. 자동 검증 범위와 수동 검증 범위를 분리해 보고하며 broad/full regression을 실행하지 않았다면 그렇게 명시한다.

