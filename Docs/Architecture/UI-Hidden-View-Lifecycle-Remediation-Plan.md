# 숨겨진 UI의 생명주기와 콘텐츠 갱신 정리 계획

## 상태와 목적

- 작성일: 2026-09-23
- 소스 검토 기준: `14337e71063bda11440d99da528818b8b090bc0e` (PR #218 병합 후 main)
- 상태: **Objective HUD 최소 수정 구현, UI/Core 자동화 통과. 수동 재현 확인 및 공통 생명주기 정리는 후속 작업.**
- 사용자 보고: 인게임 Pause에서 한국어로 변경 후 복귀했을 때 `Label_Objective`에서 일본어 글자 누락 경고 발생. 적용 폰트는 `KBODiaGothic-Light SDF`.
- 최초 조사 수준: 사용자 로그와 소스 검토. 아래 재현 후보를 실제 Pause 조작으로 실행하지 않았다. 이후 최소 수정 및 자동화 검증 결과는 9절에 별도로 기록한다.

이 문서는 [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)의 13절 책임 분리와 14절 생명주기 규칙을 구현에 연결하기 위한 supporting plan이다. canonical spec을 대체하거나 현재 구현이 이미 제안 구조를 따른다고 선언하지 않는다.

## 1. 결론: 구조 부재보다 생명주기 계약의 구현 공백

현재도 Flow, Controller, Policy, Presenter, ViewModel, View가 존재한다. 관리 코드가 여러 계층에 분산된 것 자체는 결함이 아니다. 숨김 정책, 실제 비활성화, 이벤트 구독, 콘텐츠 생성, 애니메이션 진행 조건이 서로 일관되지 않은 것이 문제다.

canonical spec은 이미 다음을 요구한다.

> Lifecycle is controller-owned and coordinator-governed.

> Destroyed or hidden UI must not continue mutating presentation state through stale subscriptions.

후속 작업은 이 계약의 구현 누락을 보완하고, 세션이 소유한 최신 표시 데이터 유지와 숨겨진 View의 시각 작업을 구분해야 한다. 단순히 모든 구독을 비활성화 시 해제하는 것으로 대체하지 않는다.

| 현재 책임 | 현재 담당 |
|---|---|
| 화면·팝업 전환 결정 | `UIFlowCoordinator`, `ScreenController`, `PopupController` |
| 화면별 HUD 표시 정책 | `ScreenPolicy.HudShellMode` |
| 실제 Unity 활성 상태 반영 | `HUDRootView`, `SettingsScreenView`, 결과 화면 View 등 |
| ViewModel 이벤트 구독 수명 | 각 View의 `Bind`, `OnDisable`, `OnDestroy` 구현 |
| 목표 행 생성·제거와 전환 | `ObjectiveHudView`, `ObjectiveHudRowView` |

Flow가 모든 `SetActive()`를 직접 호출해야 한다는 제안은 아니다. View가 정책을 실행하더라도 숨김 시 콘텐츠 작업 중단, 복귀 시 최신 상태 복원이라는 계약은 유지해야 한다.

## 2. Objective HUD에서 확인한 결함 경로

관련 소스:

- [GameplayScreenRuntimeFactory.cs](../../Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs): `CreateSettingsRuntime`
- [HUDRootView.cs](../../Assets/_Features/UI/UI_HUD/Runtime/HUDRootView.cs): `RefreshView`
- [ObjectiveHudView.cs](../../Assets/_Features/UI/UI_HUD/Runtime/ObjectiveHudView.cs): `OnDisable`, `HandleViewModelChanged`, `RefreshView`, `RefreshIdleActiveRows`, `StartEnter`, `HandleRowEnterFinished`
- [ObjectiveHudRowView.cs](../../Assets/_Features/UI/UI_HUD/Runtime/ObjectiveHudRowView.cs): `PlayEnter`, `Refresh`, `Update`
- [ObjectiveHudPresenter.cs](../../Assets/_Features/UI/UI_Application/Runtime/ObjectiveHudPresenter.cs): `HandleLocaleChanged`

소스에서 확인한 실행 가능 경로:

1. Settings 정책은 HUD를 숨기고, HUD root를 비활성화한다.
2. `ObjectiveHudView.OnDisable()`은 행을 정리하지만 ViewModel 변경 구독은 유지한다.
3. 언어 변경 시 Presenter가 최신 목표 문구를 번역하고 ViewModel을 갱신한다.
4. 비활성 View의 이벤트 핸들러가 `RefreshView()`를 호출한다. 이 경로에는 비활성 방어가 없어 `StartEnter()`로 행을 다시 만든다.
5. 부모가 비활성이므로 행의 `Update()`가 실행되지 않는다. 생성된 행은 `Entering` 상태에 머문다.
6. 다음 언어 변경은 활성 행 목록의 폰트를 갱신하지만, `RefreshIdleActiveRows()`는 전환 중인 행의 문구 갱신을 건너뛴다.
7. 복귀 시 `OnEnable()`도 같은 갱신 경로를 실행한다. 이후 진입 전환이 완료되어 최신 문구로 갱신되기 전까지 이전 문구와 새 폰트가 결합될 수 있다.

**재현 후보:** 한국어 게임 → Pause → Settings → 일본어 → 중국어 → 영어 → 한국어 → 복귀. 첫 숨김 언어 변경에서 일본어 행이 생성되고 이후 변경에서 문구가 남는 경로다. 숨김 중 전환이 정지하므로 빠른 연속 클릭이 필수 조건은 아니다. 이 순서가 사용자의 실제 조작 순서였는지는 확인되지 않았다.

첫 숨김 갱신만으로는 새 문구와 폰트가 일치한다. 위 경로에는 숨김 상태의 행 생성 이후 추가 갱신이 필요하다. 정상 Idle 행은 같은 동기 호출에서 문구까지 갱신하므로, 단순히 폰트를 먼저 바꾼다는 이유만으로 경고 원인을 확정하면 안 된다.

로그의 글자는 일본어 목표 문구 `脱出エリアに到達する（{0}/{1}）`와 일치한다. 한국어 테이블의 해당 문구는 `출구로 이동하기 ({0}/{1})`이다. Presenter와 typography binding은 같은 resolver를 사용한다. 한국어 폰트에 일본어 glyph를 추가하거나 cross-CJK fallback을 넓히는 것은 이 상태 불일치의 해결책이 아니다. 폰트 계약은 [4언어 폰트 계획](./Localization-Typography-Four-Locale-All-Resident-Plan.md)을 따른다.

## 3. 다른 UI의 조사 결과와 한계

서브 에이전트가 화면·결과 UI, 팝업, 공통 바인딩·HUD·월드 가이드를 나눠 소스 검토했다. 추가 동일 결함을 발견하지 못했다는 결과는 전체 런타임의 무결함 증명이 아니다.

| 대상 | 확인한 사항 | 판정 |
|---|---|---|
| Settings Audio/Input/Display | `OnDisable`에서 컨트롤 리스너 해제 후, 숨김 중 모델 변경의 `RefreshView`에서 다시 연결 | 생명주기 처리 불일치. 동일 glyph 오류나 사용자 영향은 미확인 |
| MainMenu 저장 슬롯 | 비활성 갱신은 있으나 문구 전체를 반영 | 같은 전환 대기 결함 미발견 |
| StageResult/GameClear/LevelFailed | 모델 변경에서 문구와 표시 상태를 반영 | 같은 문구 갱신 생략 미발견 |
| Pause | Settings 진입 시 runtime 폐기, 복귀 시 현재 locale로 새로 생성 | 숨겨진 기존 행 재사용 경로와 다름 |
| Confirm | 모델 문구와 typography 갱신, 폐기 시 구독 해제 | 같은 결함 미발견 |
| 스테이지명·월드 가이드 | 문구 갱신을 전환 완료까지 미루지 않음 | 같은 결함 미발견 |
| `LocalizedTmpTextBinding` | 문구·폰트를 한 호출에서 갱신, transient 행 생성 없음 | 구독 유지 자체만으로 결함 판정 불가 |

추적 위치:

- `UI_Screens/Runtime/SettingsAudioView.cs`: `OnDisable`, `RefreshView`, `RebindAudioControls`
- `UI_Screens/Runtime/SettingsInputView.cs`: `OnDisable`, `RefreshView`, `RebindControls`
- `UI_Screens/Runtime/SettingsDisplayView.cs`: `OnDisable`, `RefreshView`, `RebindDisplayControls`
- `UI_Screens/Runtime/SettingsScreenView.cs`: `ApplyRootVisibility`
- `UI_Screens/Runtime/StageResultScreenView.cs`, `GameClearScreenView.cs`, `LevelFailedScreenView.cs`: `RefreshView` 및 root 활성화 경로
- `UI_Composition/Runtime/GameplayPopupRuntimeFactory.cs`, `UI_Screens/Runtime/LocalizedTmpTextBinding.cs`
- `UI_Composition/Runtime/GameplayHudLocalizationBinding.cs`

위 상대 소스 경로의 공통 root는 `Assets/_Features/UI/`다.

특히 일부 화면은 모델 변경을 받아 자기 root를 다시 활성화한다. 표시 제어를 분리하기 전에 모든 View에 `OnDisable` 구독 해제 또는 비활성 `RefreshView` 반환을 일괄 적용하면, 다시 열리는 경로를 끊을 수 있다.

## 4. 목표 책임과 생명주기 계약

이 제안은 MVP를 MVVM으로 전환하거나 새로운 도메인 소유자를 만드는 계획이 아니다. 기존 계층 안에서 **화면 수명주기 제어와 콘텐츠 렌더링을 분리**한다.

| 역할 | 숨김 중 책임 | 표시 시 책임 |
|---|---|---|
| Flow/Controller 및 표시 제어 | 열기·닫기·복귀 요청과 정책 유지 | 표시 정책 전달 |
| Presenter/ViewModel | 해당 세션이 유지되는 동안 최신 표시 상태 준비 | 최신 상태 제공 |
| 콘텐츠 View | 콘텐츠 생성·전환·입력 연결을 중단하거나 갱신 필요 여부만 기록 | 최신 상태 전체 반영 후 증분 갱신 |
| View의 애니메이션 | 숨김 시 취소·정리·재개 정책 적용 | 시각 전환 수행; 문구 갱신 차단 금지 |

Presenter는 번역·매핑·의도 전달을 수행하는 로직 객체이며 DTO가 아니다. ViewModel은 표시 상태를 보관하며 현재 구현은 변경 알림도 제공한다. View는 표현 전용 애니메이션 상태를 가질 수 있지만 게임의 authoritative state를 소유하지 않는다.

제안 계약:

1. 숨김은 폐기와 다르다. 세션 상태는 유지할 수 있으나 숨겨진 콘텐츠의 행 재생성이나 전환 시작은 차단한다.
2. 표시 제어 구독과 콘텐츠 갱신 구독의 소유자를 구분한다. 다시 표시할 경로는 숨김 중에도 살아 있어야 한다.
3. 표시 시 최신 상태를 전체 반영한다. 숨김 중 발생한 모든 언어 변경을 순서대로 재생하지 않는다.
4. 문구와 폰트는 같은 locale 기준으로 렌더 전에 함께 반영한다. Entering/Exit 상태가 문구만 과거 상태로 유지하게 해서는 안 된다.
5. 재바인딩·반복 표시·폐기는 중복 구독 없이 처리한다. 세션 폐기 시 Presenter를 포함한 소유 구독을 정리한다.
6. `GameObject` 비활성화, CanvasGroup 숨김, 다른 화면에 가림, 입력 차단은 같은 상태가 아니다. 각 Policy가 콘텐츠 작업을 중단할지 먼저 정의한다. 어두워진 HUD를 무조건 숨김 취급하지 않는다.
7. 최신 상태와 일회성 연출을 구분한다. 목표 완료 연출을 숨김 중 생략할지 복귀 시 재생할지는 구현 전에 정하고, 모든 이벤트를 무조건 큐에 쌓지 않는다.

## 5. 해결책 비교

| 방안 | 장점 | 비용·주의점 |
|---|---|---|
| A. Objective HUD의 비활성 콘텐츠 갱신 차단 | 작은 변경으로 알려진 결함 경로 차단 | 다른 화면의 재발 방지 범위는 작고 콜백은 유지 |
| B. 콘텐츠 View 구독을 표시 수명에 연결 | 숨김 중 불필요한 작업 차단, 소유권 명확 | Bind/재활성화/중복 구독 정리 필요. 표시 제어 구독에 일괄 적용 불가 |
| C. 숨김 중 dirty 표시 후 복귀 시 전체 반영 | 반복 변경 병합, 캐시 화면에 적합 | 상태·예약 처리 복잡성 추가. 일회성 연출 정책 별도 필요 |
| D. 표시 제어와 콘텐츠 갱신을 공통 계약으로 분리 | 여러 화면에 일관된 규칙 적용 가능 | 변경 범위와 화면 전환 회귀 위험이 큼 |

권장 방향은 A로 시작하고, D의 책임 분리 하에서 B/C를 화면별로 선택하는 것이다. 공통 기반 클래스나 유틸리티를 먼저 강제하지 않는다. 실제 화면 수명과 사용 패턴을 정리한 뒤 공통 부분만 추출한다.

애니메이션과 문구 갱신 분리는 어느 방안에서도 필요하다. 현재 `ObjectiveHudRowView.Refresh()` 전체를 전환 중 호출하면 상태를 Idle로 바꿀 수 있다. 문구·폰트만 갱신하는 경로를 분리하여 전환 시간·진행 상태를 보존해야 한다. 퇴장 중 행의 모델/descriptor 유지 방식도 함께 정한다.

## 6. 단계별 후속 작업

- [x] **1단계 구현·자동화: Objective HUD 최소 수정.** 비활성 중 행 생성 차단, 복귀 전체 반영, 문구·폰트와 전환 상태 분리 및 회귀 테스트를 구현했다. UI/Core 결과는 9절에 기록한다.
- [ ] **1단계 수동 확인:** 실제 Pause → Settings 언어 순환 → 복귀를 실행해 화면과 경고를 확인한다. 별도 HUD PlayMode 생명주기 검증도 남아 있다.
- [ ] **2단계: Settings 리스너 수명 정리.** 콘텐츠 Refresh가 컨트롤 리스너를 다시 연결하지 않도록 책임을 분리한다. 탭 전환·입력·언어 변경을 검증한다.
- [ ] **3단계: 화면별 수명 표 작성.** 표시 제어 소유자, root 활성화 경로, 숨김 방식, 구독 시작/종료, 복귀 동기화, 애니메이션 정책을 기록한다.
- [ ] **4단계: 공통 계약 적용.** 자기 활성화와 콘텐츠 갱신이 섞인 화면부터 분리하고 B/C를 선택한다. 공통 도우미는 검증된 반복 패턴에 한해 도입한다.
- [ ] **5단계: canonical 문서와 검증 동기화.** 구현된 범위·남은 예외·검증 증거를 기록하고, 필요 시 기존 UI spec의 생명주기 세부 규칙을 보완한다.

## 7. 검증 및 완료 조건

| 검증 상황 | 기대 결과 |
|---|---|
| HUD 숨김 중 여러 locale 변경 | 행 생성·진입 전환 없음, 최신 ViewModel 유지 |
| 한국어 → Settings → 일본어 → 중국어 → 영어 → 한국어 → 복귀 | 한국어 문구와 폰트 일치, glyph 경고 없음 |
| 첫 숨김 변경 후 복귀 및 다른 시작 locale 왕복 | 숨김 전 언어나 중간 언어의 문구 잔류 없음 |
| 활성 행 Entering/Exit 중 locale 변경 | 문구·폰트 일치, 전환 진행 상태 유지 |
| 숨김 중 목표 완료·제거 후 복귀 | 최신 목표 상태 표시, 오래된 행 부활 없음; 정한 연출 정책 준수 |
| 반복 show/hide, Bind 교체, dispose | 중복 콜백·리스너 누적·폐기 후 UI 접근 없음 |
| Settings 비활성 탭에서 모델 변경 | 컨트롤 리스너 재연결 없음, 재표시 시 최신 값과 입력 정상 |
| 모델 이벤트에 의한 화면 재표시 | 표시 제어 경로 유지, 화면이 영구 비활성 상태에 남지 않음 |

문자열 또는 font 참조만 확인하는 테스트로 끝내지 않는다. 실제 TMP/Canvas 재빌드를 포함해 문구·폰트 조합과 경고를 검증한다. Unity 활성화·Update 정지의 실제 수명 검증은 적절한 통합/PlayMode 경로와 수동 Editor/Player 확인으로 보완한다.

후속 코드 변경에서는 작업 중인 worktree에서 `./run_tests.sh ui`를 실행하고, 추가 lane과 테스트 분류는 [Gameplay-Test-Automation-Guide.md](../Testing/Gameplay-Test-Automation-Guide.md)를 따른다. 기본 검증 `./run_tests.sh core` 및 필요한 targeted 검증의 실행 결과/미실행 이유를 함께 기록한다. 새 실행 증거는 `/mnt/d/J2M/evidence`에 저장한다. 과거 PR 테스트 통과는 이 재현 흐름의 검증을 대신하지 않는다.

## 8. 최초 문서화의 검증 범위

최초 문서화 당시에는 문서 및 아키텍처 색인만 추가하고 소스 경로·문서 링크·diff 형식을 점검했다. 당시 런타임·테스트·Prefab·폰트 에셋은 수정하지 않았다.

최초 문서화 때는 문서만 변경했으므로 `core`, `ui`, `full`, 수동 Unity 재현을 실행하지 않았다. 이후 코드 변경과 검증은 아래 기록으로 구분한다.

## 9. 2026-09-23 Objective HUD 최소 수정

문서화 후 사용자의 수정 지시에 따라 아래 범위를 구현했다. Flow/Settings/공통 구독 수명은 변경하지 않았다.

- `ObjectiveHudView.RefreshView`: 컴포넌트가 꺼져 있거나 부모 HUD가 숨겨져 있으면 콘텐츠를 재구성하지 않는다. 단, 목표 root의 local active 상태는 최신 모델과 맞춘다. 목표 View가 자기 root를 숨기는 구조이므로, 단순 `!isActiveAndEnabled` 반환은 목표가 다시 생길 때 복귀를 막는다. 부모가 숨겨진 동안 local active 상태 변경은 행을 생성하지 않는다.
- `ObjectiveHudRowView.RefreshContent`: 문구·폰트만 함께 적용하고 애니메이션 상태·진행 시간을 유지한다. 활성 행은 Entering/Exit 중에도 최신 모델의 콘텐츠를 적용한다.
- 모델에서 제거되어 퇴장만 남은 행은 새 번역 데이터가 없으므로 기존 문구와 기존 폰트를 함께 유지한 뒤 pool로 반환한다.
- `HUDControllerTests`의 기존 '퇴장 중 문구 동결' 기대를 최신 콘텐츠 갱신·전환 유지 계약으로 변경했다.
- `ObjectiveHudLocalizationTypographyTests`에 숨김 중 언어 순환(초기 목표 있음/없음), 진입·퇴장 중 언어 변경, 제거된 행의 폰트 보존, 빈 목표 root 재활성화 회귀 검증을 추가했다. TMP/Canvas 재빌드도 포함한다.

EditMode에서 일반 MonoBehaviour의 자동 생명주기 실행을 가정하지 않고, 테스트가 OnDisable/OnEnable을 명시적으로 호출한다. 이 검증을 실제 Pause 조작이나 PlayMode 프레임 실행의 증거로 취급하지 않는다.

실행 증거 root: `/mnt/d/J2M/evidence/objective-lifecycle-20260923`.
검증 기록:

- `CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/objective-lifecycle-20260923/ui-verified ./run_tests.sh ui`: Windows UI build 및 UI EditMode **1,615 passed / 0 failed**. 결과와 로그는 `ui-verified/test-results`, runner 로그는 `ui-verified.log`.
- 첫 UI 실행은 1,614개 중 3개 실패(기존 퇴장 문구 동결 기대 2개, 새 테스트 생명주기 호출 1개). 두 번째는 1,615개 중 새 생명주기 테스트 2개 실패. 기존 기대를 수정 목적에 맞추고 비활성 오브젝트에 전달되지 않는 SendMessage 대신 직접 lifecycle 호출을 사용한 뒤 위 최종 실행이 통과했다. 이 중간 실행을 성공 증거로 취급하지 않는다.
- `CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/objective-lifecycle-20260923/core ./run_tests.sh core`: EditMode **293 passed / 0 failed**, PlayMode **108 passed / 0 failed / 4 skipped**. 결과와 로그는 `core/test-results`, runner 로그는 `core.log`. Core PlayMode 통과를 HUD 전용 PlayMode 검증으로 해석하지 않는다.
- broad `full`은 국소 UI 변경이므로 실행하지 않았다. 수동 Pause 재현과 별도 PlayMode HUD 생명주기 검증은 미실행이다. 위 EditMode 검증이 이를 대체하지 않는다.

수동 Pause 재현과 2~5단계 공통 구조 정리는 아직 남아 있다. 이번 최소 수정은 전체 UI 생명주기 정리 완료를 의미하지 않는다.
