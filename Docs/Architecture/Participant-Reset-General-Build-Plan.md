# 참가자 초기화의 일반 빌드 통합 계획

상태: 2026-09-07 일반 빌드 통합 코드 반영. 검증 결과와 미실행 항목은 [일반 빌드 통합 검증](../Testing/Participant-Reset-General-Build-Validation.md)을 따른다.
대상 worktree: `/mnt/d/J2M/worktrees/exhibition-reset`.
기존 구현: [전시 초기화 계획](Exhibition-Participant-Reset-Implementation-Plan.md), [검증 기록](../Testing/Exhibition-Participant-Reset-Validation.md).
이 문서는 일반 빌드 통합의 동작 계약이다. 기존 전시 빌드 문서와 검증 기록은 과거 구현의 기록으로 보존한다.

## 1. 이번 구현 범위

- 일반 Windows 빌드에 초기화 기능을 포함한다. 별도 전시 build 명령과 `J2M_EXHIBITION` 없이 Unity Build 및 기존 release CLI로 만든다.
- 일반 MainMenu에 `다음 참가자 준비` 버튼을 둔다. F10과 같은 기존 popup/input 흐름을 사용하되 **F10 창에 새 항목 추가는 이번 범위 밖**이다.
- 초기화는 메뉴 준비 완료 시에만 요청한다. 게임 진행 중 저장을 삭제하지 않는다.
- 현재 기능 범위는 Steam 업적과 현재 canonical 저장 경로의 전체 참가자 진행을 함께 초기화하는 것이다. Local provider에서도 게임과 메뉴는 정상 실행하고, 해당 버튼에는 Steam 사용 필요 사유를 표시한다. 별도 Local-only 삭제 기능을 추가하지 않는다.
- Editor에서도 같은 UI와 Controller를 사용한다. 실제 재실행 수명만 Editor adapter로 분리하며 자동 테스트에서는 fake Steam/restart 및 임시 저장소를 주입한다.
- 고정 SteamID 설정은 제거한다. 사용자가 초기화 요청할 때의 실제 AppID/SteamID를 Pending에 저장한다.
- 범용 서비스 프레임워크, 추가 승인 단계, 계정 허용목록, 자동 재시도 정책은 만들지 않는다.

## 2. 동작 계약

### 일반 시작

1. SubsystemRegistration에서 feature의 정적 상태·구독을 초기화한다.
2. AfterAssembliesLoaded에서 초기화 저널만 확인한다. 기록 없음 또는 정상 완료 Ready면 즉시 통과한다. 과거 Ready의 계정이나 mapping 차이로 정상 실행/다음 요청을 막지 않는다.
3. Pending이 없으면 Steam 연결·계정 검사를 초기화 기능 때문에 수행하지 않고 기존 업적·메뉴 시작을 유지한다.
4. Pending이면 ProductAchievement 시작과 Steam publication을 BeforeSceneLoad 이전에 보류한다. platform/native 초기화와 callback pump는 계속 실행한다.
5. MainMenu의 공통 UI shell(Canvas/EventSystem/PopupController/필요한 navigation)을 먼저 구성한다. Pending 완료 전에는 BuildSaveSlotModule 및 이에 의존하는 Hub를 생성하지 않는다.
6. Pending 처리 완료 후에만 업적 서비스 시작, 메뉴 저장 모듈 구성, campaign reconciliation을 완료하고 버튼 조작을 허용한다. 오류는 이미 준비한 공통 UI로 표시한다.

### 초기화 요청

1. 기존 Confirm popup으로 `현재 저장 경로의 모든 슬롯·업적 장부와 현재 Steam 계정의 대상 업적을 초기화하며 재시작한다`고 안내한다. 설정은 보존한다.
2. 메뉴 준비 상태와 중복 실행 여부를 확인하고, Steam 연결/현재 identity 및 restart adapter 사용 가능 여부를 확인한다.
3. 현재 identity와 기존 mapping version을 Pending에 저장한다.
4. Pending이 기록되면 정상 조작과 업적 전송을 중지하고 한 번 재실행한다.
5. 새 세션에서 Pending identity를 확인한 후 대상 업적 Clear → StoreStats 성공 callback → 미획득 readback을 확인한다.
6. 전체 슬롯·활성 진행·업적 장부·복구 백업·재획득 근거를 비우고 검증한다. Ready를 저장한 뒤 정상 서비스를 시작한다.

Ready는 완료 기록이다. 계정 고정 정책으로 쓰지 않는다. Pending은 진행 중인 삭제 요청이므로 계정/AppID/mapping 일치를 유지한다.

### 실패 처리

| 시점 | 동작 |
| --- | --- |
| Pending 기록 전 사용 불가/취소 | 이유를 표시하고 일반 메뉴 유지 |
| Pending 저장 후 재실행 실패 | Pending 유지, 정상 플레이를 재개하지 않고 재실행/종료 안내 |
| 재실행 후 계정 불일치/Steam 실패 | 로컬 삭제 없이 Pending 유지, 원인 해소 후 명시적 재실행 |
| Steam 초기화 후 로컬 삭제 실패 | Pending 유지, 다음 재실행에서 동일 초기화 반복 |
| Ready 저장 중 예외 | canonical 기록 재조회. Pending이면 재실행 복구, Ready가 반영됐으면 완료 기록을 기준으로 처리 |
| Ready 완료 | 기존 메뉴 및 업적 서비스를 정상 시작 |

FileExhibitionResetJournal.Save는 원자 교체 이후 companion 정리에서 예외가 날 수 있다. Save 예외를 곧바로 기록 전 실패로 취급하지 않는다. canonical 기록을 다시 읽어 실제 반영된 Pending/Ready를 판단하며, 판독 실패 시 정상 플레이를 재개하지 않는다. 새로운 다단계 저널을 추가하지 않는다.

## 3. 파일별 작업

| 작업 | 대상/구현 내용 | 완료 기준 |
| --- | --- | --- |
| 요청 identity로 일반화 | `ExhibitionResetCoordinator.cs`, `SteamExhibitionResetAdapter.cs`: 생성자 고정 identity 제거, 요청 시 기록, Pending에서만 검증 | 다른 계정 Ready와 Steam 미연결 정상 시작 가능 |
| 시작 분리 | `ExhibitionApplication.cs`의 책임을 feature startup/service로 옮김. 무조건 hold와 JSON 설정 읽기 제거 | 저널 없음에서는 기존 시작 경로, Pending에서만 보류 |
| 메뉴 조립 분리 | `MainMenuUiFlowInstaller.cs`: 공통 shell과 저장/Hub 조립 분리 | Pending 완료 전에 캠페인 저장소 생성/복구가 일어나지 않음 |
| 좁은 UI 연결 | `UI_Application/Runtime/MainMenuPorts.cs`에 선택적 참가자 초기화 서비스 계약, 메뉴 composition에 주입 | UI가 Steam·파일·process 구현을 직접 호출하지 않음 |
| 버튼 및 확인 | `MainMenuScreenModels.cs`, `MainMenuScreenView.cs`, `MainMenuHubController.cs`, `MainMenuScreen.prefab`, 메뉴 localization | 기존 Confirm popup 재사용, 메뉴 전환 차단·키보드 탐색 준수 |
| 복구 상태 표시 | 기존 popup shell 아래 진행/오류 상태 View/Controller 연결 | Pending 중 재시도·종료 가능, 별도 EventSystem/Canvas 생성 없음 |
| 재실행 분리 | restart interface, Windows Player adapter, Editor adapter | Player는 게임만 재실행, Editor는 Unity 종료 없이 Play Mode 세션 교체 |
| 일반 빌드 연결 | feature Editor assembly의 Windows build 후처리, release artifact 계약/staging 검증 | Unity Build와 release CLI 모두 helper를 포함 |
| 전용 구조 정리 | `ExhibitionBuild.cs`, Boot 씬, 전용 define, `exhibition.json` 의존, `ExhibitionView`와 전용 modal input/navigation 제거 | 하나의 메뉴·popup·입력 체계로 실행 |

어셈블리 순환을 피한다. 현재 Exhibition.Integration → UI.Composition 의존 상태에서 반대 참조를 추가하지 않는다. UI.Application에 좁은 계약 및 선택적 연결점을 두고 feature가 구현/등록하도록 한다. 등록 부재 시 일반 메뉴는 동작하고 초기화 명령만 사용할 수 없다. 초기화 알고리즘과 Steam/저장/restart adapter는 feature 경계에 남긴다.

메인메뉴 Prefab 변경의 목적은 버튼·레이아웃·탐색 순서를 에디터에서 확인할 수 있게 하는 것이다. 확인창은 기존 ConfirmPrefab을 재사용한다. 진행/오류 View도 같은 PopupLayer와 입력 소유자를 사용한다. 새 PopupId는 기존 Confirm/상태 표시로 표현할 수 없는 경우에만 추가한다.

## 4. Player 및 Editor 시작 조건

- 일반 Steam 배포에서 `steam_appid.txt`는 staging sanitizer에 의해 제외된다. 재실행 helper가 이 파일에 의존하지 않도록 현재 요청의 AppID를 자식 실행 환경에 전달하는 방식을 구현·검증한다. 게임 실행 인자는 Steam provider를 유지한다. 이 기능은 Local→Steam 강제 전환을 제공하지 않는다.
- helper 포함은 일반 Windows build 후처리에 연결하고 release manifest 생성 전에 끝낸다. 정상 배포 계약에는 helper를 포함하되 임의 ps1 전체 허용으로 확장하지 않는다.
- Editor restart adapter는 기존 Editor direct-play의 SessionState/playModeStateChanged 패턴을 참고한다. Unity.exe 재실행 및 Application.Quit를 사용하지 않는다.
- 초기화 재시작은 MainMenu로 들어온다. Editor direct-play의 이전 스테이지 재진입보다 Pending 복구가 우선한다. 임시 play-start-scene 변경이 필요하면 원래 설정을 복원한다.
- Domain Reload 비활성에서도 정적 보류/등록/구독 상태가 다음 Play 세션에 남지 않도록 기존 SubsystemRegistration 패턴을 사용한다. `CampaignSaveCompositionProvider.productionComposition`은 현재 production 세션 초기화 훅이 없으므로 해당 캐시 및 launch 상태의 수명 처리를 추가하고, 이전 세션 객체가 재사용되지 않는지 검증한다. 테스트 전용 reset API를 제품 경로에서 호출하지 않는다.
- Editor Play Mode 재시작은 프로세스 교체가 아니다. 기존 Steam native 종료 및 callback 해제가 세션 종료 시 완료되는지 검증한다. 이 검증을 Windows의 실제 프로세스 교체 증거로 대체하지 않는다.
- 과거 전시 빌드가 남긴 Pending은 같은 저널 형식과 대상 검증으로 재개한다. 새 기능을 끄거나 빌드가 바뀌었다는 이유로 무시하지 않는다.

## 5. 구현 순서 및 검증

1. **Coordinator·저널 계약**: 없음/Ready 무Steam 호출, 요청 identity 저장, Pending mismatch 삭제 없음, 저장 전후 실패 분기 테스트.
2. **일반 시작 연결**: early Pending 보류와 MainMenu 두 단계 조립. 정상 시작 즉시 통과, Pending 중 save/reconciliation 선실행 없음 테스트.
3. **공통 UI 연결**: 메뉴 버튼·기존 확인창·복구 표시와 입력 연결. 확인/취소, transition 차단, Pause/F10 실제 클릭, 키보드 탐색 검증.
4. **재실행·일반 Build**: Windows/Editor adapter와 helper 포함, AppID 전달. 앱ID 파일 없는 자식 실행 환경 및 Play Mode 재진입 테스트.
5. **기존 전시 구성 제거**: 위 연결 완료 후 전용 define/Boot/build/config/UI 제거. 파일 및 .meta를 함께 관리하고 테스트 기대값을 일반 포함으로 변경.
6. **실제 일반 Steam Player 검증**: 현재 계정 업적 획득 → 버튼 확인 → 한 번 재실행 → 빈 진행/미획득 API 확인 → 같은 업적 재획득. 다른 계정 정상 시작, 연결 불가 정상 메뉴도 확인.

필수 lanes: 해당 worktree의 `./run_tests.sh core`, `./run_tests.sh ui`, startup/reset/save/restart touched-cluster focused tests. 그래픽 포인터 테스트는 그래픽 실행과 기존 합성 입력 포커스 설정을 사용한다. Prefab 목적과 Editor/Player 수동 결과를 기록하며, 광범위 full lane 미실행이면 별도로 표시한다.

새 build 출력은 `/mnt/d/J2M/builds`, 증거는 `/mnt/d/J2M/evidence`에 둔다. 기존 C 저장소 및 사용자 변경은 이동·되돌리지 않는다. 초기화 요청 없이 실제 Steam 업적이나 사용자 저장을 테스트 준비 용도로 삭제하지 않는다.

## 6. 재검토 결과

서브 에이전트가 수명/저장 순서와 UI/build/Editor 통합을 독립 검토했다. 다음 지적을 계획에 반영했다.

- define만 제거할 때 업적 시작이 영구 보류될 수 있음.
- 메뉴 Install 전체를 대기시키면 실패 안내용 popup도 없어짐: shell 먼저, 저장 모듈 나중으로 분리.
- Ready 계정 검사가 일반 실행을 막음: Pending에만 대상 일치 적용.
- UI.Composition 역참조를 추가하면 assembly 순환이 생김: 계약을 통한 연결.
- 기존 helper 및 appid 파일은 일반 Steam staging과 다름: 일반 build 포함 및 자식 AppID 경로 검증.
- Editor 조기 반환만 삭제하면 Unity 프로세스를 재실행하려 함: Editor 수명 adapter 필요.
- 저널 Save 예외가 commit 이후일 수 있음: canonical 재조회로 분기.

문서 수준의 재검토다. 최종 문서 재검토에서는 Editor의 production campaign 캐시/launch 상태와 Steam native/callback 종료 검증 보강을 지적받아 반영했다. 위 재검토는 구현 전 문서 검토 기록이다. 후속 구현 및 테스트 결과는 일반 빌드 통합 검증 문서에 별도로 기록한다.
