# 참가자 초기화 일반 빌드 통합 검증

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


날짜: 2026-09-07. Worktree: `/mnt/d/J2M/worktrees/exhibition-reset`.
작업 사본 검증이며 커밋/배포하지 않았다. 기존 전시 구현 및 사용자 변경을 유지한 상태에서 일반 통합 변경을 적용했다.

## 구현

- 일반 Windows/Editor startup에서 저널만 확인한다. 없음/Ready는 Steam identity 검사 없이 통과한다. Pending/판독 실패만 업적 publication과 production 저장 조립을 보류한다.
- MainMenu shell과 저장/Hub 조립을 분리했다. 공통 Confirm popup으로 요청·진행·오류를 표시한다. UI.Application 선택적 port로 feature에 연결하며 역방향 assembly 참조를 추가하지 않았다.
- 요청 당시 identity/mapping을 Pending에 기록하고 한 번 재시작한다. canonical 저널을 다시 읽어 원자 교체 이후 Save 예외를 판별한다. Steam Clear/Store callback/readback 이후 전체 참가자 진행과 복구 근거를 삭제한다. 설정은 보존한다.
- Ready는 완료 기록이며 계정 고정 정책이 아니다. Local provider에서는 일반 메뉴를 유지하고 초기화 버튼에 Steam 연결 필요 사유를 표시한다.
- canonical 저장 루트별 프로세스 잠금을 일반 Steam/Local 및 Editor에 적용한다. Player는 프로세스 종료까지, Editor는 Play 객체 종료 후까지 잠금을 유지한다.
- Windows helper는 요청 AppID를 자식 환경 SteamAppId/SteamGameId에 전달하고 Steam provider 인자를 유지한다. 일반 build postprocessor와 배포 artifact allowlist에 정확한 helper만 추가했다.
- Editor adapter는 SessionState/playModeStateChanged로 Play를 교체하고 MainMenu를 우선하며 원래 playModeStartScene을 복원한다. production campaign cache와 feature registration/hold는 SubsystemRegistration에서 초기화한다.
- 별도 전시 Boot/config/build/define/modal UI를 제거했다. 기존 저널 이름/형식 및 Pending 호환성은 유지한다.

## Prefab 및 폰트 목적

MainMenuScreen.prefab에 네 번째 명령 `다음 참가자 준비`를 추가했다. 기존 세 버튼의 navigation index를 유지하고 새 명령을 마지막에 배치했다. 긴 한국어 명령/Steam 필요 문구를 위해 명령 패널 크기를 조정했다. 확인/진행/오류는 기존 ConfirmPrefab과 PopupLayer를 사용한다.

메뉴 문자열 8개를 en-US/ko-KR 및 localization bootstrap에 추가했다. canonical KBO glyph updater로 Medium/Light SDF를 갱신했다. glyph missing/fallback은 0이며 GUID/atlas identity를 유지했다. 폰트 무결성 검증은 사용자 index를 변경하지 않는 별도 validation.index에 후보 font blob만 넣어 수행했다.

## 자동 검증

증거 루트: `/mnt/d/J2M/evidence`.

| 검증 | 결과 | 증거 |
| --- | --- | --- |
| reset/startup/save/Steam/build 집중 EditMode | 445 통과, 실패 0 | `participant-reset-general-focused-verified/test-results/wsl-unity-full-editmode.xml` |
| Steam host 수명 PlayMode | 7 통과, 실패 0 | 동일 루트 `wsl-unity-full-playmode.xml` |
| helper 실제 Windows 자식 실행 (fake exe) | 통과: AppID 123456, Steam provider 인자, 공백·apostrophe·$ 경로, appid 파일 없음 | `participant-reset-helper/result.log` |
| core | EditMode 254 통과; PlayMode 107 통과, 그래픽 전용 4 skip, 실패 0 | `participant-reset-core-final/test-results` |
| 배포 staging PowerShell wrapper | 9 통과, 실패 0 | `participant-reset-staging-tests.log` |
| SDF 무결성 guard fixture | 통과 | `participant-reset-sdf-guard.log` |
| 공통 Pending 오류 popup 실제 포인터 클릭 | 1 통과, 실패 0 (graphics) | `participant-reset-menu-pointer-v3/test-results/wsl-unity-full-playmode.xml` |
| KBO glyph updater | 통과, missing/fallback 0 | `participant-reset-general-glyphs-final` |

집중 lane 명령: `UNITY_GRAPHICS=1 ./run_tests.sh full --filter 'Game.Exhibition.Tests;Game.Platform.Steam.Tests;CampaignSaveFacadeTests;CampaignSaveSlotStoreAdapterTests;WindowsReleaseBuildPipelineTests;WindowsDistributionStagerTests'`.
각 lane에서 `CODEX_VALIDATION_ROOT`를 D 증거 경로로 지정했고 폰트 후보 검증용 `GIT_INDEX_FILE=/mnt/d/J2M/evidence/participant-reset-general-source/validation.index`를 사용했다.

초기 실패는 artifact 기대값, localization bootstrap/문자 수, EditMode prefab OnEnable 호출, 생성된 csproj의 오래된 Compile 항목과 공유 obj 경로에서 발생했으며 수정 후 재검증했다. 새로운 PlayMode assembly가 Editor 전용으로 분류되어 집중 실행에서 누락된 것을 별도로 발견하여 수정했다. 포인터 검증은 raycast와 실제 합성 입력을 모두 검사하며 기존 테스트와 동일한 IgnoreFocus/GameView 입력 설정을 사용한다.

## 실연동 및 광범위 검증 경계

실제 Steam 계정의 획득→요청→재실행→미획득 readback→재획득, 다른 실제 계정 전환, 실제 네트워크 단절은 실행하지 않았다. 사용자 저장이나 실제 Steam 업적을 테스트 준비 목적으로 삭제하지 않았다. fake Steam/재시작 및 임시 저장소 테스트를 실연동 증거로 간주하지 않는다.

범위 전체 full lane은 실행하지 않았다. `full --filter` 결과는 집중 범위 검증이며 project-wide/full green을 의미하지 않는다. Editor adapter의 수동 버튼 재시작 및 설정 복원, Prefab의 다양한 화면 비율/언어 육안 검토는 자동 계약 검사와 구분한다.

초기 `participant-reset-editor-session`은 EditMode 동기 runner가 UnityTest를 제외해 양쪽 모드 0건으로 끝났으므로 세션 수명 증거가 아니다. graphics batch의 초기 ScreenCapture 결과는 검은 화면이어서 육안 검증 증거로 사용하지 않는다.

## 추가 수명 및 오류 경로 검증

`UNITY_EDITMODE_ASYNC=1 UNITY_GRAPHICS=1 ./run_tests.sh full --filter 'ParticipantEditorSessionTests;ParticipantMenuPreviewTests;Game.Exhibition.Tests;MainMenuHubTests'`는 `participant-reset-final-cluster-v2`에서 EditMode 93건과 PlayMode 1건이 모두 통과했다. `UNITY_EDITMODE_ASYNC`는 기본 동기 lane을 유지하면서 EnterPlayMode/ExitPlayMode UnityTest를 실행하는 선택적 runner 옵션이다.

Domain Reload 비활성 실제 Play 진입→종료→재진입에서 production cache sentinel 제거, 저장 접근 hold 초기화, 이전 fake Steam callback 정지, overlay callback dispose/native shutdown 각 1회를 확인했다. 실제 Steam native DLL과 Editor adapter 버튼 재시작의 수동 실연동 결과로 대체하지 않는다.

Ready 반영 후 메뉴 저장 모듈 조립 실패도 서비스에 전달하도록 보완했다. 메뉴를 다시 열지 않고 publication을 중지하며, 오류창에서 명시적 재시작이 가능하다. 회귀 테스트는 Ready 유지와 삭제 반복 없이 restart adapter 호출을 검사한다.

## Prefab 육안 검토

기존 Editor TypographyPreviewScreenshotUtility로 1920×1080 한국어/영어 MainMenu를 렌더링했다. 첫 캡처에서 긴 새 명령의 잘림을 확인해 새 버튼의 preferred width 520/height 88, 좌우 text margin 20과 영어 안내 줄바꿈을 적용했다. 최종 이미지에서 명령과 Steam 필요 사유가 모두 버튼 내부에 표시되는 것을 확인했다. 기존 세 명령의 크기와 navigation index는 유지한다.

- `/mnt/d/J2M/evidence/participant-reset-prefab-final/test-results/participant-menu-preview/MainMenu_ko-KR.png`
- `/mnt/d/J2M/evidence/participant-reset-prefab-final/test-results/participant-menu-preview/MainMenu_en-US.png`

이는 authored Prefab의 Editor 렌더링이며 실제 Player 배경 합성이나 모든 화면 비율 검증은 아니다.

## 최종 결과

- `./run_tests.sh ui`: **1,359 통과 / 실패 0**, `participant-reset-ui-verified-final/test-results/wsl-unity-ui-editmode.xml`.
- 세션 경계 최종 보강 후 `UNITY_EDITMODE_ASYNC=1 ./run_tests.sh full --filter 'ParticipantEditorSessionTests;ParticipantResetServiceTests;SteamExhibitionResetProtocolTests'`: **EditMode 26 통과 / 실패 0**, `participant-reset-session-final/test-results/wsl-unity-full-editmode.xml`. 이전 세션 adapter의 identity 접근을 새 세션에서 거부하는 실제 재진입 검사를 포함한다. 이전 세션의 늦은 비동기 continuation이 새 Steam 연결로 진행하지 못한다.
- release wrapper PowerShell 테스트: **145 통과 / 실패 0**, `participant-reset-release-wrapper-tests-final.log`. 처음 긴 D 임시 경로에서 fixture preflight 1건이 실패했으며 짧은 `D:\Repositories\prt` 경로로 재실행해 통과했다. 테스트용 Git 저장소도 D Repositories 하위에 생성했다.
- `WindowsReleaseBuildCli.BuildWindowsX64NonDevelopment`: **exit 0**, Windows x64 Store Mono, 일반 MainMenu/UIAudio 씬 구성. 마지막 source에서 빌드한 결과는 `/mnt/d/J2M/builds/participant-reset-general/.staging-final`이며 build report/settings transaction/log는 `/mnt/d/J2M/evidence/participant-reset-general-build-final`에 있다.
- `Stage-WindowsDistribution.ps1`: **통과**, 252 files / 381,498,042 bytes. helper 원본/산출물 SHA-256 일치, Steam native/managed 각 1, `steam_appid.txt` 0, denied artifact 0.
- 최종 payload: `/mnt/d/J2M/builds/participant-reset-general/steam-final/payload`.
- canonical staging 증거 사본: `/mnt/d/J2M/evidence/participant-reset-general-build-final/staging`. 배포 artifact 자체의 companion manifest/SUCCESS도 함께 보존했다.
- manifest SHA-256: `4858f36126400e45f1b10891ca66b472bf49c1a13c9a21420c68f1abe173a77b`.

Build는 기존 release CLI와 BuildPipeline 후처리를 사용했다. 공통 release notice 게시 함수 및 정상 Steam staging sanitizer를 적용했다. 커밋하지 않은 작업 사본이므로 메타데이터의 sourceSha/sourceTree는 `uncommitted-working-copy`로 명시했다. clean detached source 생성·공식 release promotion/배포는 수행하지 않았다. PowerShell release wrapper의 별도 required-artifacts 계약도 C# 계약과 동일하게 helper를 포함하도록 갱신했다.

core는 앞의 결과 이후 gameplay/core 변경이 없으며, 마지막 service/adapter 보강은 위 집중 검사로 재검증했다. 실제 계정 전체 초기화/재획득, Editor adapter 수동 버튼 재시작(일반 Domain Reload 포함), Pause/F10의 별도 그래픽 수동 클릭, 다양한 화면 비율 검토는 미실행이다. core의 4개 skip은 모두 별도 그래픽 lane을 요구하는 기존 테스트다.

## 독립 재검토에서 확인한 미해결 사항

일반 Steam 메뉴 버튼 경로에서 빈번하게 발생하는 장애로 확인된 사항은 아니지만, 다음 두 P2 경계 문제는 이번 커밋에서 수정하지 않았다.

- Windows Player를 명시적으로 `-batchmode`로 실행하면 `ExhibitionApplication`의 batch mode 조기 반환으로 Pending 확인과 초기화 보류가 생략된다. 일반 실행과 재실행 helper는 이 인자를 추가하지 않는다. 제품 Player의 Pending 확인을 테스트 실행 제외 조건과 분리하는 후속 보강이 필요하다.
- Pending이 남은 Editor에서 Direct Play의 `CampaignProductionSlot`을 선택하고 Overwrite를 확인하면, Play Mode 진입 전 production 저장소 생성·복구·슬롯 기록이 발생할 수 있다. Editor restart adapter의 ExitingEditMode 확인보다 앞선 접근이다. 배포 Player에는 없는 개발 도구 경로이며, production 저장 준비 전에 Pending을 확인하는 후속 보강이 필요하다.

위 문제 때문에 모든 시작 경로에서 Pending 보호가 완료됐다고 주장하지 않는다. 실제 Steam 초기화·재획득 및 수동 Editor adapter 재시작 검증 공백도 유지한다.
