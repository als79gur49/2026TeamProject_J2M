# 전시 참가자 초기화 구현 검증

대상: `feature/exhibition-reset`, `/mnt/d/J2M/worktrees/exhibition-reset`.
계약과 실행 방법: [구현 계획](../Architecture/Exhibition-Participant-Reset-Implementation-Plan.md).

## 변경 목적

전시 계정에서 참가자가 바뀔 때 메뉴 확인 → Pending 저장 → 한 번 재실행 → 대상 Steam 업적 및 모든 캠페인 슬롯·업적 장부·복구 파일 초기화 → Ready 저장 → 정상 업적 서비스 최초 시작을 제공한다. 설정과 진단 파일을 보존한다. 일반 빌드에는 전시 runtime assembly와 Boot scene을 포함하지 않는다.

`ExhibitionBoot.unity`는 전시 빌드의 첫 scene이며 전용 composition root와 KBO 원본 폰트를 참조한다. 공유 MainMenu scene, Prefab, ScriptableObject 및 공유 폰트 에셋은 변경하지 않았다. 전시 UI는 원본 폰트로 별도 동적 TMP atlas를 소유하여 새 한국어 문구를 표시한다.

## 자동 검증

검증 runner는 이 worktree의 `./run_tests.sh`를 사용한다. 실행 전 resolved path가 `/mnt/d/J2M/worktrees/exhibition-reset`임을 확인했고, `CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/exhibition-reset`로 증거를 분리했다.

| 검증 | 결과 | 근거 |
| --- | --- | --- |
| `./run_tests.sh core` | EditMode 254 passed, PlayMode 107 passed / 4 skipped | `test-results/wsl-unity-core-*.xml`, `core-runner.log` |
| `./run_tests.sh ui` | 1,356 passed | `test-results/wsl-unity-ui-editmode.xml`, `ui-runner.log` |
| focused `./run_tests.sh full --filter ...` | EditMode 396 passed, PlayMode 7 passed | `test-results/wsl-unity-full-*.xml`, `focused-runner.log` |
| Windows helper fake E2E | passed | 아래 Windows 검증 |

증거 상대 경로는 `/mnt/d/J2M/evidence/exhibition-reset/` 기준이다. focused 필터는 다음과 같다.

```text
Game.Exhibition.Tests;Game.Platform.Steam.Tests;Game.Product.Achievements.Tests;CampaignSaveSlotStoreAdapterTests;CampaignSaveServiceTests;FileCampaignProfileRepositoryTests;AtomicTextFileStoreTests
```

집중 테스트에는 새 전시 테스트 43개와 Steam maintenance 테스트 9개가 포함된다. 정상 Player compilation의 전시 assembly 제외, 기본 release scene 유지, 새 한국어 글리프 생성, 모달 입력 차단·복원, Pending/Ready, 저장 backup/rollback, schema/StoreStats/timeout/readback 및 callback 소유권을 검증했다. 테스트 과정에서 종료 시 callback 해제 누락, 실패 시 callback 소유권 재사용, 잘못된 시작 성공 판단, 일반 Steam 초기화의 중복 로그인 조회를 수정했다.

초기 UI 검사에서 Boot의 씬 이름 하드코딩과 직접 scene load가 발견되어 기존 RouteConfig 사용 및 전시 Boot만의 문서화된 최초 로드 예외로 수정했다. 기본 UI 검사에서 다른 경로에 대한 기존 제약은 유지한다. 새 설치의 profile 부재가 반환하는 `NotAttempted`도 정상 시작으로 허용했다.

Core PlayMode의 4개 skip은 그래픽 장치가 필요한 기존 RenderTexture/화면 연속성 증거 테스트다. 이번 headless 실행에서 그 시각 검증을 통과했다고 주장하지 않는다. 공통 폰트 무결성 검사에서는 공유 Light/Medium SDF 변화가 없었다. 검증 대상 소스·에셋 해시는 `source-sha256.json`에 기록한다.

## Windows 재실행 도우미

무해한 C# fake executable로 Windows PowerShell helper를 실행하여 다음을 확인했다.

- 정확한 부모 PID·UTC 시작 시각으로 부모가 살아 있는 동안 자식을 실행하지 않는다.
- 부모가 종료된 뒤 자식을 한 번 실행한다.
- 공백이 있는 실행 파일 경로에서 인자가 `-j2mPlatformProvider steam`이며 작업 디렉터리는 실행 파일 디렉터리다.
- helper 종료 코드가 0이다.

증거: `/mnt/d/J2M/evidence/exhibition-reset/relaunch-test/20260906-161940-704/result.json`, 같은 `relaunch-test/README.md` 및 `verify-relaunch.ps1`. 실제 게임, Steam 계정, 참가자 저장에는 접근하지 않았다. 첫 harness는 Windows Process.ExitCode handle 관측 문제로 실패하여 harness를 수정한 뒤 다시 통과했다. 제품 helper는 변경하지 않았다.

## 미실행과 운영 확인

- 실제 Steam `ClearAchievement`·StoreStats·재획득·팝업·재부팅 후 유지: 실행하지 않았다. 후속 빌드에는 기존 설치 AppID·계정을 적용했으나 이번 요청은 빌드 생성으로 한정하여 실제 게임 및 초기화를 실행하지 않았다. fake 성공은 Steam 서버 영속성 증명이 아니다.
- 실제 전시 Windows x64 Player 빌드: 아래 후속 빌드에서 성공. 첫 Boot → MainMenu 화면·입력·초기화 버튼 실운영은 미실행이며, scene/font/입력의 자동 테스트와 별도로 Windows 전시 PC에서 확인해야 한다.
- 모듈을 물리적으로 제거한 Player 빌드·부팅·저장·업적: 미실행. 일반 Player assembly 제외 검증을 이 실운영 검증과 혼동하지 않는다.
- 광범위 full lane: 실행하지 않는다. focused full 필터 실행은 touched-cluster 근거다.

실운영 순서는 참가자 A의 대상 업적 획득 → 초기화 확인 → 한 번 재실행 → 빈 슬롯 및 초기화된 업적 확인 → 참가자 B의 같은 업적 재획득·팝업 → 재부팅 후 B 진행 유지다. 네트워크 실패·Pending 중단·Ready 후 시작 실패에서도 실패 세션에 머물고 수동 재시도만 하는지 확인한다. 초기화는 여러 저장소에 걸친 단일 트랜잭션이 아니며 Pending 상태에서 전체 작업을 재시도한다.

## 후속 수정: 메뉴 복귀 시 EventSystem 복구

서브 에이전트 재검토에서 게임 UI가 비활성화한 전시 소유 EventSystem을 메뉴 복귀 때 그대로 재사용하는 경로를 확인했다. MainMenu의 준비 검사는 활성 EventSystem을 요구하고, 후속 UI 생성은 그 검사 이후이므로 복귀가 대기 상태에 남을 수 있었다.

`ExhibitionView.EnsureEventSystem()`은 활성 대체 객체가 없으면 기존 전시 소유 GameObject와 EventSystem을 활성화하고 현재 입력 소유자로 지정한다. 활성 대체 객체가 있으면 기존 양보 동작을 유지한다. 공유 MainMenu 및 게임 UI 코드는 변경하지 않았다.

`ExhibitionEventSystemTests`는 게임용 객체가 사라진 뒤 비활성 전시 객체를 재사용하는 경우(컴포넌트 비활성 / GameObject 비활성)와 활성 대체 객체에 양보하는 경우를 검사한다. 이는 PlayMode의 입력 객체 수명 회귀 테스트이며 실제 Player의 화면 전환 왕복 증거는 아니다.

후속 증거 루트: `/mnt/d/J2M/evidence/exhibition-event-system-fix`.

- 전시 전용 테스트: PlayMode 3 passed (`./run_tests.sh full --filter ExhibitionEventSystemTests`). EditMode 0건은 해당 fixture가 PlayMode 전용이기 때문이며, 성공 근거는 PlayMode XML이다.
- 전체 UI lane: 1,356 passed (`./run_tests.sh ui`).
- Core 및 광범위 full: 이번 후속 수정에서는 미실행. UI 입력 소유권 수정으로 범위를 한정했다. 위 초기 구현 결과와 구분한다.
- 최초 `ui --filter ExhibitionEventSystemTests`는 UI lane의 고정 assembly 범위 때문에 0건으로 실패했으며 검증 근거로 사용하지 않는다. 전시 test assembly를 포함하는 focused full로 다시 실행한다.

초기 EditMode 회귀 시도는 EventSystem의 실행 수명 등록이 없어 실패했다. 실제 OnEnable/OnDisable을 사용하는 전용 PlayMode test assembly로 옮겨 3건 모두 통과했다.

## 후속 전시 Player 빌드 (2026-09-07 KST)

Unity 6000.3.11f1에서 `Game.Exhibition.Editor.ExhibitionBuild.BuildWindows`로 Windows x64 Player를 생성했다. Unity 종료 코드 0, 로그 `Build Finished, Result: Success.` 및 전시 구성 파일 발행을 확인했다. 빌드 디렉터리 이름은 UTC 시각 기준이다.

- 산출물: `/mnt/d/J2M/builds/exhibition-20260906-174623/VectorQuake.exe`와 같은 디렉터리의 전체 파일.
- 첫 실행: 같은 폴더의 `Start-Exhibition.cmd`가 `-j2mPlatformProvider steam`을 전달한다. 기존 Steam 계정 로그인이 필요하다.
- AppID `5218360`; 설치 appmanifest의 LastOwner와 PC의 유일한 Steam 등록 계정이 일치함을 확인하여 기존 SteamID를 적용했다. `exhibition.json` 및 `steam_appid.txt` 일치를 검증했다.
- 전시 Application/Integration Player DLL, Steamworks.NET 바인딩과 `steam_api64.dll`, 재실행 helper 원본 일치, Addressables catalog·bundle 포함을 확인했다. Addressable content 빌드도 성공했다. Managed 출력에 테스트 DLL은 없다.
- 증거: `/mnt/d/J2M/evidence/exhibition-build-20260907/`의 `unity-build-20260906-174623.log`, `build-location.json`, `artifact-verification.json`. 후자는 필수 파일 해시와 구성 일치 결과를 기록한다.
- 이번 작업에서는 제품 코드를 변경하지 않아 테스트 lane을 다시 실행하지 않았다. 기존 자동 테스트 결과와 이번 Player 빌드 성공을 구분한다. 실제 게임 실행·Steam 업적 초기화·재획득은 수행하지 않았다.

기존 재검토의 P2 후보(메뉴 복귀 준비 완료 전 초기화 버튼 노출과 모달 입력 소유권 전환)는 이번 빌드에서 수정하지 않았다. 메뉴 전환 완료 후 사용하는 운영 안내를 산출물에 포함했다. 실제 Player 재현 및 개선 검증은 남아 있다.

## 후속 수정: 전시 입력 액션 소유권 및 숨김 UI (2026-09-07)

Steam 전시 빌드에서 F10 창과 Pause 버튼의 클릭이 전달되지 않는다는 실운영 제보를 조사했다. `ExhibitionView`가 기본 입력 액션을 공유하는 InputSystemUIInputModule을 생성하고, 다른 EventSystem에 양보할 때 기존 모듈을 비활성화하는 경로가 있었다. 프로젝트의 Input System 1.19.0은 이때 공유 기본 액션을 해제한다.

수정 전 focused PlayMode에서는 전시 소유자 제거 후 게임 입력 모듈의 포인터 액션 참조 소실을 재현했다(4건 중 기존 3건 통과, 새 회귀 1건 실패). 증거: `/mnt/d/J2M/evidence/exhibition-pointer-fix-before/test-results/wsl-unity-full-playmode.xml`. 이는 입력 전환 결함의 재현이며, 실제 F10/Pause 화면을 자동 조작한 증거는 아니다.

전시 EventSystem을 비활성 상태에서 구성하고 모듈 전용 `DefaultInputActions` 인스턴스와 액션 참조를 연결한 뒤 활성화한다. 제거 시 전시 소유 입력만 정리한다. 전시 UI가 숨겨지면 GraphicRaycaster도 비활성화하고, 메뉴 버튼이나 모달 표시 시에만 다시 활성화한다. 공유 게임 UI, 씬, Prefab 및 Steam 초기화 경로는 수정하지 않았다.

회귀 테스트는 다른 활성 EventSystem을 일시적으로 격리·복원한다. 기존 소유권 복구/양보 검사와 함께 숨김 Raycaster 상태 및 양보 후 포인터 이벤트가 실제 uGUI Button.onClick에 도달하는지 검사한다. 첫 수정 후 실행은 입력 액션 유지 검사까지 통과했으나 테스트가 Mouse 버튼에 float delta를 전달하여 실패했다. 테스트를 MouseState 이벤트로 수정했으며, 이 중간 실행은 최종 성공 근거로 사용하지 않는다(`/mnt/d/J2M/evidence/exhibition-pointer-fix`).

후속 클릭 harness 실행에서도 4건 통과 / 클릭 1건 실패가 있었으며, 각각 `/mnt/d/J2M/evidence/exhibition-pointer-fix-final`과 `/mnt/d/J2M/evidence/exhibition-pointer-fix-graphics`에 보존한다. 그래픽 실행에서 버튼 Raycast 도달과 액션 유지는 통과했으나 클릭 입력은 전달되지 않았다. 저장소의 기존 합성 입력 테스트와 동일하게 테스트 동안 background IgnoreFocus 및 Editor AllDeviceInputAlwaysGoesToGameView를 설정하고 종료 시 복원한다. 제품의 포커스 정책은 바꾸지 않는다.

최종 focused 검증: `UNITY_GRAPHICS=1 CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/exhibition-pointer-fix-verified ./run_tests.sh full --filter ExhibitionEventSystemTests` — PlayMode **5 passed**, EditMode 0건(PlayMode 전용 fixture). 양보 후 실제 합성 마우스 입력이 uGUI Button.onClick에 전달되는 회귀를 포함한다. `test-results/wsl-unity-full-playmode.xml`, `focused-runner.log`, `source-sha256.json`을 증거로 보존한다.

이번 변경은 전시 UI 입력 수명에 한정하여 core 및 광범위 full lane은 재실행하지 않는다. 실제 Steam Player의 F10/Pause 화면 조작, 수정 Player 재빌드 및 Steam 재업로드는 아직 수행하지 않았다. 위 테스트 성공은 설치된 기존 BuildID 25155063이 수정되었다는 뜻이 아니다. 앞서 기록한 메뉴 준비 완료 전 버튼 노출 P2 후보는 별도 잔여 항목이다.

동일 소스의 전체 UI lane: `CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/exhibition-pointer-fix-verified ./run_tests.sh ui` — **1,356 passed**. 근거: `test-results/wsl-unity-ui-editmode.xml`, `ui-runner.log`. 최종 소스 해시 일치와 `git diff --check`도 확인했다.
