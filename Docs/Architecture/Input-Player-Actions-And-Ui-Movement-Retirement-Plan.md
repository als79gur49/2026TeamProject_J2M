# Player 미사용 액션 및 UI 이동 명령 경로 제거 설계

- 상태: A/B1/B2 구현과 `main`의 플레이어 재생성 제거 계약을 통합해 자동 검증했다. 수동 Editor/Player 조작 검증은 미실행이며 전체 close 조건은 아직 충족하지 않았다. 1~6절은 원래 실행 설계, 7절은 통합 전 기록, 8절은 현재 통합 결과다.
- 작성일: 2026-09-25 KST.
- 원 설계 검토 기준: `c9b765235c03b5fde281c1f0f62f8488cf7c2bd1`. 8절의 통합 기준은 PR head `e21707597`과 `main` `2ffc71ffc`다.
- 목적: 제품 소비자가 없는 Player 액션 7개와 UI 이동 명령 경로를 제거하면서 기존 키 설정, 실제 게임 입력, HUD query 및 Host 수명주기를 보존한다.
- 실행 단위: A(액션 에셋), B1(policy 소유권 이전), B2(UI 이동 경로 제거). A와 B는 독립적이며 B2는 B1 이후에 수행한다.

## 1. 보존 계약과 제거 범위

| 분류 | 내용 |
| --- | --- |
| StrongContract — 보존 | Move/Push/Flip 입력 의미, Push/Flip binding ID 기반 저장 호환성, 이동 버퍼와 입력 시각 소유권, Pause·terminal·scene-entry·presentation gate와 사망 후 영구 입력 차단, completed snapshot freshness, presentation-only UI 경계 |
| 명시적으로 폐기할 공개면 | `IGameplayCommandGateway`, Context/FlowPorts의 `CommandGateway`, UI held-direction 전달. 기존 architecture/type-shape 테스트는 새 공개면에 맞춰 이관한다. |
| CurrentPolicy — 보존 | 맵 enable/disable 복구, 0.125초 이동 버퍼, 물리 키 입력 순서와 Push/Flip 방향 캡처, 현재 Pause의 일반 입력 보존 방식 |
| 제거 대상 에셋 데이터 | Player/Look, Attack, Crouch, Jump, Previous, Next, Sprint 및 이들에 속한 바인딩 |

`GameplayUiDirection`은 presentation slice에서 사용하므로 유지한다. `GameplayUiAccessMapper.ToUiDirection`도 유지하며 Gateway만 사용하는 역변환 `ToGameplayDirection`을 제거한다. Shared/UI 키 설정 DTO와 `KeyboardBindingSettingsPortAdapter`, `IKeyboardBindingStore`, `PlayerMoveIntentBuffer`, `KeyboardMoveOrderTracker`, `TickInputBuffer`를 유지한다.

Reflection 두 곳의 단순화, UI navigation router/InputSystemUIInputModule 역할 변경, 입력 장치 지원 확대, Input System 패키지 업그레이드는 이 설계 범위에 포함하지 않는다.

## 2. A — Player 액션 에셋 제거

### 2.1 정확한 변경 범위

대상은 `Assets/InputSystem_Actions.inputactions`의 Player 맵이다.

| 삭제 액션 | action ID | 연결 binding 수 |
| --- | --- | ---: |
| Look | `6b444451-8a00-4d00-a97e-f47457f736a8` | 3 |
| Attack | `6c2ab1b8-8984-453a-af3d-a3c78ae1679a` | 6 |
| Crouch | `27c5f898-bc57-4ee1-8800-db469aca5fe3` | 2 |
| Jump | `f1ba0d36-48eb-4cd5-b651-1c94a6531f70` | 3 |
| Previous | `2776c80d-3c14-4091-8c56-d04ced07a2b0` | 2 |
| Next | `b7230bb6-fc9b-4f52-8b25-f5e19cb2c2ba` | 2 |
| Sprint | `641cd816-40e6-41b4-8c3d-04687c349290` | 3 |
| 합계 | 7 actions | 21 |

변경 전 Player는 10 actions/36 bindings, 변경 후에는 Move/Push/Flip 3 actions/15 bindings다. UI 맵의 10 actions/38 bindings는 그대로 둔다. 바인딩 삭제는 Player 맵 안의 `binding.action`을 정확히 대조한다. 다른 에셋의 Attack/Jump 애니메이션이나 Previous/Next UI 버튼을 이름 검색만으로 삭제하지 않는다.

아래 데이터는 값과 상대 순서를 보존한다.

- InputActionAsset `.meta` GUID `052faaac586de48259a63d0c4782560b` 및 importer 설정(`generateWrapperCode: 0`). `.meta`를 재생성하지 않는다.
- Player/UI map ID, 잔존 action ID, 잔존 binding ID와 각 binding의 path/groups/interactions/processors/composite 정보.
- Move의 composite 및 gamepad/joystick binding, Push의 gamepad binding, 전체 UI 맵, 모든 control scheme.
- MainMenu/UIAudioScene과 `ProjectSettings/EditorBuildSettings.asset`의 전체 에셋 참조.

저장 호환성의 핵심 ID는 Push 키보드 `1c04ea5f-b012-41d1-a6f7-02e963b52893`, Flip 키보드 `f6403135-b3d4-4300-bf3e-9a0ae3dbec40`다. 삭제로 달라지는 배열 인덱스 대신 ID로 보존 여부를 비교한다. 삭제 집합을 제외한 JSON 트리의 구조적 동등성을 확인하고, orphan binding과 중복 ID가 없는지 검사한다.

제품 소비자가 없다는 근거와 비활성 상태는 구별한다. 프로젝트 전역 actions 활성화 및 `KeyboardBindingSettingsService.WithManagedMapsDisabled`의 맵 복구로 삭제 대상도 활성화될 수 있다. 이번 변경은 그 액션들의 존재와 활성화 자체를 없애지만 Move/Push/Flip/UI의 관찰 가능한 기능은 보존해야 한다.

### 2.2 삭제 전 저장 JSON 확보

기존 테스트의 임의 생성 에셋 간 clone 로드는 production asset의 삭제 전후 호환성 증거를 대신하지 못한다. A의 에셋 편집 전에 아래 절차를 수행한다.

1. 기준 SHA의 production `.inputactions`와 `.meta`를 D 드라이브 evidence에 보존한다. 현재 SHA-256은 각각 `76d6b375a9e8276d652fdf313fa229f549ea741b4317cf34e341f7fb40f5ff1b`, `3b4e424a80f5b4fa76b6abd774884a24285eaa61f22ff9bb20e5ad505fd8b569`다. 실행 시 값이 달라졌으면 기준을 다시 고정한다.
2. production 에셋을 메모리에서 복제하고 fake `IKeyboardBindingStore`와 실제 `KeyboardBindingSettingsService`를 사용한다. 가상 Keyboard를 통해 `StartRebind`를 완료하여 Push=R, Flip=T를 저장한다. private serializer를 복제하거나 새 에셋에서 JSON을 만들어 구버전 fixture라고 부르지 않는다.
3. Wasd/ArrowKeys 각각에서 저장된 JSON, movement scheme, 에셋 hash, 생산 revision, 생성 절차를 기록한다. 두 키는 두 이동 scheme과 충돌하지 않는다. 사용자 PlayerPrefs에는 쓰지 않는다.
4. 생성된 작은 JSON payload는 기존 `KeyboardBindingSettingsServiceTests`의 고정 fixture로 보존한다. 테스트 실행 시 변경 후 에셋의 ID로 다시 생성하지 않는다. 원본 production 에셋 전체를 영구 테스트 에셋으로 추가할 필요는 없다.

### 2.3 삭제 후 호환성 검증

- Unity import 후 production 에셋을 불러와 복제하고, 위 고정 JSON을 fake store에서 로드한다.
- Push/Flip의 `effectivePath`가 R/T이며 snapshot 표시와 두 이동 scheme의 Player/Move 및 UI/Navigate 경로가 맞는지 확인한다.
- 유효한 구버전 JSON 로드가 `ClearBindingOverridesJson`이나 기본값 fallback을 호출하지 않는지 기록형 fake store로 확인한다. 예외를 삼킨 채 기본값으로 돌아간 결과를 성공으로 처리하지 않는다.
- 재바인딩 저장→새 service에서 재로드, 기본값 복원(J/K 및 Wasd), 손상 JSON의 기존 fallback 계약을 확인한다.
- 기존 저장 키 `Game.Feature.Input.KeyboardMovementScheme`, `Game.Feature.Input.KeyboardBindingOverridesJson`과 JSON 형식을 유지한다. ID가 보존되므로 새 저장 버전·migration code·사용자 설정 초기화는 필요하지 않다.
- 실제 Move/Push/Flip, MainMenu/Pause의 navigation/Submit/Cancel, pointer click/scroll을 확인한다. 삭제 대상의 Enter/Space/마우스 binding과 살아 있는 UI binding이 겹치므로 UI 입력도 검증한다.

## 3. B1 — admission policy의 소유권 이전

현재 생성 위치는 `GameplayHostRuntimeFactory`이며 같은 policy를 Gateway, Session query, Player HUD query, SurfaceButton query가 공유한다. 현재 해제 체인은 다음과 같다.

```text
GameplaySceneHost.OnDestroy
  -> GameplayHostUiAccessContext.Dispose
    -> GameplayHostCommandGateway.Dispose
      -> GameplayHostCommandAdmissionPolicy.Dispose
        -> GameplayInputHost.TickCompleted 구독 해제
```

새 소유자는 기존 Host 수명에 묶인 `GameplayHostUiAccessContext`다. FlowPorts/Installer/화면 닫기에서 policy를 Dispose하지 않는다.

```text
GameplayHostRuntimeFactory
  -> policy 1개 생성 -> 기존 query들과 Gateway에 동일 인스턴스 주입
  -> Context에 policy 수명 소유권 전달

GameplaySceneHost.OnDestroy
  -> Context.Dispose
    -> policy.Dispose
    -> presentationFeed.Dispose
```

### 3.1 구체적인 API 변경

- `GameplayHostUiAccessContext` 생성자에 필수 `IDisposable admissionPolicyLifetime` 인자를 추가하고 private readonly 필드로 보관한다. null은 거절한다. 내부 policy 타입을 public API로 노출하거나 수명 owner용 새 인터페이스를 만들지 않는다.
- Factory는 query가 사용하는 바로 그 `admissionPolicy`를 인자로 전달한다. policy를 하나 더 만들지 않는다.
- Context에 `_isDisposed` guard를 두고 중복 Dispose를 no-op으로 만든다. policy를 먼저 해제하고 `finally`에서 기존 disposable presentation feed도 해제한다.
- `GameplayHostCommandGateway`에서는 `IDisposable`과 `Dispose()`를 제거한다. B1 동안 이동 명령과 admission 평가는 유지한다. Context와 Gateway가 함께 owner인 중간 상태를 남기지 않는다.
- policy/feed 생성 후 Context/runtime 반환 전에 구성 실패가 발생하면 아직 이전되지 않은 생성 완료 객체를 Factory가 해제한다. 이 범위의 실패 cleanup만 다루며 SceneHost 전체 재초기화 설계로 확장하지 않는다.
- public Context를 직접 만드는 `CampaignStageFlowTests`는 필수 수명 인자를 제공한다. lifecycle 전용 테스트는 호출 수를 기록하는 disposable을 사용한다. UI ports에는 이 인자를 전달하지 않는다.

### 3.2 B1의 완료 조건

기존 policy 자체 Dispose 테스트는 별도 `CreatePolicy(host)` 인스턴스를 검증하므로 production owner 이전의 증거로 충분하지 않다. 다음을 함께 검증한다.

- Context의 반복 Dispose와 이후 Host 파괴에서 policy/feed 해제가 각각 한 번 실행된다.
- 실제 Factory가 만든 policy는 정상 TickCompleted 때 snapshot을 갱신하며, Context.Dispose 뒤에는 더 갱신하지 않는다. 기존 테스트 접근 방식 또는 Infrastructure 전용 reflection을 사용하고 production public probe는 추가하지 않는다.
- UI Installer의 해제만으로 Host 소유 policy가 해제되지 않는다.
- query 세 소비자가 기존 snapshot window, committed actor, Pause/terminal gate 결과를 유지한다.
- 구성 실패 후 소유권이 없는 `TickCompleted` 구독이 남지 않는다.

## 4. B2 — UI 이동 경로 제거

### 4.1 파일별 변경

아래 경로에서 Host는 `Assets/_Features/Gameplay/Gameplay_Host/Runtime`, UIAccess는 `Assets/_Features/Gameplay/Gameplay_UIAccess/Runtime`을 뜻한다.

| 파일/영역 | 변경 |
| --- | --- |
| UIAccess `Contracts/IGameplayCommandGateway.cs` | 파일과 `.meta` 제거 |
| Host `UIAccess/GameplayHostCommandGateway.cs` | 파일과 `.meta` 제거 |
| Host `UIAccess/GameplayHostUiAccessContext.cs` | Gateway 생성자 인자·검증·property 제거. B1 수명 필드는 유지 |
| Host `GameplayHostRuntimeFactory.cs` | Gateway 생성 제거. policy 단일 생성·query 주입·Context 소유권 유지 |
| UI `UI_Application/Runtime/GameplayUiFlowPorts.cs` | Gateway 생성자 인자·검증·property 제거 |
| UI `UI_Composition/Runtime/GameplayUiFlowInstaller.cs` | FlowPorts에 넘기는 Gateway 인자 제거 |
| Host `GameplayInputHost.cs` | `_uiHeldMoveDirection`, 초기화, Set/ClearUiHeldMoveDirection, ClearPendingUiInput와 호출 제거 |
| Host `UIAccess/GameplayHostPauseService.cs` | UI 전용 clear 호출만 제거 |
| Host `UIAccess/GameplayUiAccessMapper.cs` | `ToGameplayDirection` 제거. `ToUiDirection` 및 나머지 매핑 유지 |

`GameplayInputHost.BuildPlayerCommand`의 방향 결정은 `_moveIntentBuffer.ResolveDirection(now, out usesBufferedDirection)`과 `_moveIntentBuffer.HeldDirection`으로 단일화한다. `ResolveHeldDirectionAtActionPress`는 `ResolveSampledMoveDirection()`을 사용한다. 입력 업데이트 전 방향 snapshot과 Push/Flip 캡처 타이밍은 유지한다. UI setter만 쓰는 `IsOrthogonalDirection` helper는 참조 0을 재확인한 뒤 제거한다.

`ClearPendingUiInput` 호출은 terminal 진입, scene-entry/terminal 초기화, respawn, Pause에서 제거하되, 기존 `ClearPendingPlayerInput`, tick 누적 시간 초기화, 입력 gate는 유지한다.

**Pause에서 UI clear를 `ClearPendingPlayerInput`으로 바꾸지 않는다.** 현재 `SetSimulationPaused`는 flag만 변경하고 일반 입력 버퍼를 비우지 않는다. UI 경로 제거를 이유로 키보드의 hold/release·버퍼 수명·재개 동작을 바꾸면 별도 기능 변경이 된다.

### 4.2 명령 결과 DTO 정리

Gateway 제거 후 `EvaluateActionableRequest()`와 `GameplayCommandAcceptance`는 제품 호출자가 없어진다. 같은 B2에서 전역 참조를 재확인하고 method와 DTO 파일/`.meta`를 제거한다. 정책 테스트는 기존 `CanAcceptActionableCommands(out reason)`을 사용한다.

`GameplayCommandRejectionReason`과 policy의 `CanAcceptActionableCommands` overload는 유지한다. policy/query gate와 테스트의 진단 vocabulary를 이번 제거와 섞어 변경하지 않는다. `GameplaySessionReadModel.CanAcceptGameplayCommands` 역시 실제 UI readiness 계약이므로 유지한다. policy 이름 변경과 남은 enum 값 정리는 별도 필요성이 생길 때 검토한다.

### 4.3 테스트 이관

| 기존 테스트/구조 | 이관 방식 |
| --- | --- |
| `GameplayUiAccessRuntimeTests`의 presentation/topology/snapshot 테스트 | `Actions == null`인 기존 harness에서 `SetRawMoveInput(Vector2...)`로 이동을 만들고 기존 tick·HUD·presentation assertion을 유지 |
| `GameplayHostCommandAdmissionPolicyTests`의 command 거절 assertion | policy의 bool/reason 및 Session query를 검사. 이동이 필요한 snapshot 테스트는 raw input 사용 |
| `GameplayUiAccess_PauseRejectsActionableCommands_AllowsClear_AndClearsPendingUiInput` | 폐기되는 Set/Clear acceptance assertion을 제거하고 Pause 중 tick/presentation 차단 및 depth 검증을 유지. 일반 입력 Pause/Resume 계약은 별도 characterization으로 확인 |
| `PlayerHudRead_CountDoesNotChangeReleasedInputCommand` | `uiHeld` override case만 폐기. query 0/1/다회, release 직후 버퍼, 0.125초 경계/만료 case 보존. 물리 held direction의 query 불변성은 유지하거나 보강 |
| `ActualSceneBootstrapSmokePlayModeTests`의 Gateway reflection 호출 | 기존 raw Move/BufferPush/BufferFlip 차단 검증을 유지하고 Gateway 부분 제거. 실제 production action asset을 가진 Host의 입력은 가상 Keyboard 상태 이벤트로 검증 |
| `UiTestDoubles.FakeGameplayCommandGateway`, `CampaignStageFlowTests.NoOpGameplayCommandGateway` | fake와 생성/전달 인자 제거 |
| `GameplayUiAccessArchitectureTests`의 assembly anchor | 살아 있는 `IGameplayQueryFacade`로 변경 |
| `UiArchitectureTests`, `MainMenuSettingsOverlayTests` | 삭제 타입의 `typeof`/허용 목록을 갱신하고 UI→Host/authoritative state 직접 의존 금지 검증 유지 |

새로운 테스트 전용 UI 이동 진입점은 만들지 않는다. 실제 `InputActionAsset`이 연결된 Host에서는 `RefreshMoveInputFromAction()`이 raw 입력을 덮을 수 있으므로 raw 주입만으로 production 입력 검증을 대체하지 않는다.

제거 이후 runtime에는 Gateway 타입/보관/반사 조회, UI held field와 setter/clear, 역방향 mapper 참조가 없어야 한다. 삭제를 확인하는 테스트의 이름 문자열과 역사 문서는 검색 결과에서 구분한다. 테스트 수 감소는 폐기 case와 이관 case 목록으로 설명하며 기존 검증 수치를 미리 바꾸지 않는다.

## 5. 실행·검증·완료 기준

### 5.1 실행 순서

1. A의 삭제 전 production fixture를 확보하고 기존 에셋에서 복원 성공을 확인한다.
2. A의 에셋 필터 삭제와 compatibility/asset 테스트를 구현한다. UI import 및 입력 smoke까지 확인한다.
3. B1의 owner 이전을 구현·검증한다. 이 단계는 Gateway가 남은 상태에서 독립적으로 성립해야 한다.
4. B2의 제거와 테스트 이관을 함께 구현한다. 최종 통합 revision에서 core/ui 및 touched-cluster 결과를 확보한다.

커밋하는 경우 각 A/B1/B2는 별도 의도로 나눈다. Scene/Prefab 변경은 예상하지 않으며 import가 만든 무관한 변경은 포함하지 않는다. 삭제되는 Unity C# 파일은 `.meta`와 함께 제거한다. 현재 존재하는 untracked `Assets/AddressableAssetsData/Windows.meta`, `ProjectSettings/ScriptableBuildPipeline.json`은 이 작업에 포함하지 않는다.

### 5.2 검증 명령과 범위

모든 lane은 검증 대상 worktree에서 `./run_tests.sh`로 실행한다. 새 evidence는 `/mnt/d/J2M/evidence`, 새 Player build는 `/mnt/d/J2M/builds` 아래에 둔다. 각 명령에 고유 하위 디렉터리를 사용해 결과를 덮어쓰지 않는다. 예시의 run ID는 실행 당시 생성한 값으로 대체한다.

```bash
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/input-retirement-RUN/a-bindings ./run_tests.sh ui --filter KeyboardBindingSettingsServiceTests
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/input-retirement-RUN/b-policy ./run_tests.sh full --filter GameplayHostCommandAdmissionPolicyTests
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/input-retirement-RUN/b-uiaccess ./run_tests.sh full --filter GameplayUiAccessRuntimeTests
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/input-retirement-RUN/final-core ./run_tests.sh core
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/input-retirement-RUN/final-ui ./run_tests.sh ui
```

추가로 architecture/Context lifecycle fixture, `CampaignStageFlowTests`, 변경한 실제 씬 smoke를 지정 실행한다. 새 lifecycle fixture 이름은 구현 시 확정하고 실제 실행 명령과 선택된 test 이름을 기록한다. 각 필터의 matching test가 0인 경우 검증 성공으로 세지 않는다.

production asset/저장 호환성 검증은 UI EditMode, public surface·DI·reflection 검증은 Infrastructure, Host를 구성하고 tick을 실행하는 검증은 Extended, 실제 씬 입력·수명주기는 PlayMode에 둔다. 순수 이동 버퍼의 결정적 검증만 Core 후보로 다루며 새 테스트를 편의상 Core에 넣지 않는다.

입력 PlayMode는 최소한 re-enable/rebind, 짧은 Move release 버퍼, Push/Flip 단독 키, 같은/다른 InputUpdate의 방향 전환, interaction lock, terminal/scene-entry/respawn 차단을 포함한다. 기존 `PlayerMovementPlayModeTests`의 해당 case를 선택하며 fixture 전체를 실행할 경우 graphics 관련 실패를 입력 성공으로 덮어 보고하지 않는다.

실제 씬 수동/Editor 검증은 MainMenu→gameplay→Pause→resume, Wasd↔ArrowKeys, R/T 저장 후 재진입, 기본값 복원, Submit/Cancel 및 pointer click/scroll을 기록한다. Pause hold/release 결과는 변경 전후 동일 조건으로 비교한다. B의 수명 검증에는 UI 재설치와 Host 파괴를 구별한 절차를 포함한다.

Player build를 실행하는 lane에서는 `PLAYER_BUILD_ROOT=/mnt/d/J2M/builds/input-retirement-RUN` 등 해당 lane의 build-root 옵션도 명시한다. 새 worktree가 필요할 때만 저장 정책에 따라 `j2m-worktree-add`, D 여유 공간 30 GiB 확인, resolved project path와 private Library 확인을 수행한다.

### 5.3 문서 동기화 및 완료 기준

- 구현 후 `Immediate-Push-Input-Semantics.md`의 UI-held/Gateway 문구와 관련 runtime 구조 설명을 최종 소스에 맞춘다. 이 문서의 E 기본키 등 기존 설명도 현재 J/K·방향 캡처 소스와 대조해 정정하되, 오래된 문서에 코드를 맞추지 않는다.
- `UI-Current-Structure-Source.md` 및 canonical UI architecture에 새 경계가 필요한지 확인한다. 과거 deletion audit/validation report는 실행 당시 기록으로 보존한다.
- `Gameplay-Test-Automation-Guide.md`와 UI baseline에는 실제 추가/삭제/이관 내역 및 측정한 결과만 기록한다.
- A 완료: 정확한 7/21 삭제, 잔존 JSON/ID 보존, Unity import, 삭제 전 production JSON 복원, 필수 자동·수동 입력 검증 통과.
- B 완료: policy owner 단일화와 구독 해제 검증, UI 이동 경로 제거, query·presentation·실제 입력 계약 검증 통과, 폐기 테스트의 이관 근거 기록.
- evidence에는 revision, source hash, 실행 명령, XML/log, 수동 검증 결과, 실행하지 않은 항목과 이유를 남긴다. 수동 검증을 하지 못했으면 해당 완료 조건은 미완료로 남긴다.
- broad unfiltered full을 실행하지 않으면 core/ui 및 targeted 결과만 보고한다. 기존 2026-09-24 결과는 변경 후 검증을 대신하지 않는다.

## 6. 실패 시 복구 범위

A의 호환성/import 실패는 이 작업의 에셋·테스트 변경만 되돌려 원본 ID를 복구한다. 사용자 저장 설정을 삭제하거나 binding ID를 다시 생성해 우회하지 않는다. B2 실패는 B2만 복구하여 B1의 Context 소유권을 유지할 수 있다. B1을 복구할 때에는 Context와 Gateway 사이 owner 변경을 한 단위로 되돌려 중복 owner 또는 owner 없음 상태를 방지한다.

설계 작성 시 수행한 것은 소스/참조 검사와 JSON 인벤토리 확인이다. 런타임·에셋 수정, 저장 fixture 생성, core/ui/PlayMode/수동 검증은 아직 실행하지 않았다.


## 7. 구현 및 검증 기록 — 2026-09-25

- A: 7 actions/21 bindings만 제거했다. 나머지 JSON 값·순서, inputactions meta는 원본과 동일하다. 삭제 전 production 에셋 복제본에서 실제 `StartRebind`로 저장한 R/T JSON을 고정 fixture로 남겼다.
- B1: Context가 단일 policy owner이며 반복 Dispose를 막고 feed 해제를 finally로 보장한다. Factory 구성 실패의 policy/feed 구독 해제도 검증했다. 이 중간 상태는 `b1-change.patch`와 source hashes로 보존했다.
- B2: Gateway 계약·구현·주입, UI held-direction, 역변환 mapper, EvaluateActionableRequest/acceptance DTO를 제거했다. policy/query, rejection reason, presentation 방향 DTO, 키 설정 계층과 실제 입력 버퍼는 유지했다. UI 재생성은 Host policy를 해제하지 않는다.
- 테스트: UI 5 cases 추가 및 architecture guard 1개 이름 변경. Gameplay lifecycle 4 cases, Pause 3 cases 추가. UI-held case 1개를 제거하고 physical-held case 1개로 이관했다. presentation/snapshot/campaign 테스트의 기존 의미 assertion을 유지하면서 입력 진입점을 변경했다. 실제 씬 smoke는 기존 어셈블리 경계를 유지하도록 Session query를 reflection으로 확인한다.

| 실행 | EditMode | PlayMode | 해석 |
| --- | --- | --- | --- |
| A 삭제 전 production rebind/save | 2 passed | 미실행 | 원본 JSON 생성·재로드 |
| A 삭제 후 키 설정 fixture | 22 passed | 미실행 | 고정 구버전 JSON, 재바인딩, 기본값, 손상 설정 |
| B1 policy/Context/Pause | 14 passed | 0 selected | Gateway 제거 전 소유권 이전 확인 |
| B2 UIAccess/policy/architecture/campaign/UI lifetime | 184 passed | 0 selected | query·입력·수명 이관 확인 |
| 입력·실제 씬 선택 | 7 passed | 38 passed | production asset 4 cases 및 실제 씬 handoff 포함 |
| `core` | 293 passed | 108 passed, 4 skipped | skip은 그래픽 장치/전용 렌더 evidence 필요 |
| `ui` | 1620 passed | 미실행 | Windows build 포함 |

초기 A 후속 빌드는 고정 fixture 상수 연결 전 컴파일 오류가 있었고, B2 첫 빌드는 PlayMode 어셈블리의 직접 UIAccess 참조 오류가 있었다. 둘 다 수정한 재실행이 통과했으며 최초 로그도 보존했다. 실행 과정의 code/source-category 경고와 4개 graphics skip은 입력 회귀로 합산하지 않는다. 모든 실행의 font guard는 `NO_MUTATION`이다.

코드 검증 뒤 문서 수치와 두 문서 guard literal을 실측 `1620`으로 동기화했다. 그 문서 변경은 별도 UI documentation 필터로 검증하며 결과는 evidence summary에 기록한다. core/전체 UI/input 선택 사이에는 런타임·입력 테스트 소스 변경이 없다.

- evidence root: `/mnt/d/J2M/evidence/input-retirement-20260925-000826/`
- 세부 명령·XML·log·hash·초기 실패: [validation summary](/mnt/d/J2M/evidence/input-retirement-20260925-000826/validation-summary.md)
- 소스 기준: `c9b765235c03b5fde281c1f0f62f8488cf7c2bd1` + 보존된 working-tree patch/source hashes. 이 작업에서는 커밋하지 않았다.
- 미실행: 사람의 실제 키보드/마우스 Editor·Player smoke, broad unfiltered full, 별도 graphics/render lanes 및 Player build. 자동 씬/가상 장치 테스트를 수동 조작 증거로 간주하지 않는다.
- 보존: 기존 untracked 두 파일과 inputactions meta의 SHA-256은 작업 시작과 동일하다. 삭제한 C# meta GUID의 Assets/ProjectSettings 잔여 참조는 0이다.

## 8. `main` 통합 — 2026-09-25

`main`의 플레이어 재생성 제거(`3b3b35b79`) 이후 임시 respawn-delay 차단은 현재 계약이 아니다. `GameplayInputHost`는 사망 신호를 받으면 입력과 후속 tick을 영구 차단하고, 그 전까지 Push/Flip 입력 시점 방향 캡처와 행동 재생 잠금을 유지한다. 사망 결과 처리에서는 행동 잠금 갱신 후 사망 차단이 pending 입력을 지운다. UI held-direction 경로는 복구하지 않는다.

Factory의 구성 실패 `try/catch`와 Host Context의 admission-policy/feed 소유권을 유지하면서, `main`이 제거한 respawn timing 및 Context 생성자 인수는 되살리지 않았다. 자동 병합 테스트의 폐기된 Gateway 더블은 `NoOpLifetime`으로 이관했다. 기존 사망·캠페인 테스트에는 사망 후 Move/Push/Flip 재입력과 자동/수동 tick 차단 검증을 추가했다.

- 같은 통합 working tree에서 `./run_tests.sh core`: EditMode `293 passed / 0 failed`, PlayMode `109 passed / 4 graphics skips / 0 failed`.
- `./run_tests.sh full --filter PipelineDestroyTileDeathAndObjectiveClear_FeedCommitsDefeatOnceAndBlocksInput`: EditMode `1 passed / 0 failed`, PlayMode `0 selected`.
- Factory policy와 Push/Flip 입력 10개 사례의 filtered `full`: EditMode `11 passed / 0 failed`, PlayMode `10 passed / 0 failed`.
- `./run_tests.sh ui`: Windows build와 EditMode `1623 passed / 0 failed`.
- 증거: `/mnt/d/J2M/evidence/pr221-main-integration-20260925/`. 최초 Core 실행은 오래된 Unity 생성 `.csproj`가 제거된 RespawnProcessor 파일을 참조해 Windows build 전에 중단됐다. 생성 파일을 증거 폴더로 옮긴 뒤 runner의 cold-checkout import 경로로 재실행해 최종 Core가 통과했다.
- 미실행: 수동 Editor/Player 입력·사망 전환, 별도 Player build, 그래픽 전용 검사, 필터 없는 broad `full`. 따라서 통합된 자동 검증 범위만 주장한다.
