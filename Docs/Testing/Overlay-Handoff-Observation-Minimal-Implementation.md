# Overlay 관찰 최소안 구현 기록

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


2026-09-09. 대상 `/mnt/d/J2M/worktrees/exhibition-reset`.
기준은 [필수 기능만 유지하는 수정안](./Overlay-Handoff-Observation-Correction-Plan.md)이다. 기존 선택 기능 유지안과 Build25203840의 캡처 흐름은 구현 요구에 합치지 않는다.

## 로컬 구현

- 캡처/import, 별도 요청 시각, Overlay sampling/추가 activation 기록, frame/graphics/module 조사, Steam 로그, 업적 getter와 SDK 문서를 제거했다. 보조 상태·비동기 작업·오류 채널·5분 만료 및 해당 테스트/compiled 검사 의존도 제거했다.
- 사용자 보고는 역할당 `user-observation.json` 하나다. `Source=User`, run/role/process identity, AttemptReported, Visibility, UTC 및 프로세스 단조 시각을 저장한다. opened/not-visible은 열기를 시도했다는 진술이며 inconclusive는 확인 불충분이다.
- 보고 저장과 파일 재검증 대기 중 종료를 허용한다. 반환 후, claim 뒤, helper 생성 직전에 취소/core 상태를 검사한다. 이미 저장한 사용자 진술과 소비한 claim은 보존하며, 수락 후 원본 종료는 교체 진행이다.
- 원본 opened 저장이 교체 전제다. 장시간 관찰 후에도 계정·Ready·참가자 파일·config/payload·동일 Steam client를 재검증한다. native/receipt 준비 및 기존 GameOnly 부모 대기 30초와 `StartGame(null)`을 유지한다.
- Request/Context/Receipt는 최소 관찰 v2다. 준비 시 원본 context에는 미래 보고를 요구하지 않고, replacement context에는 원본 보고 경로/hash를 필수로 둔다. helper/child가 보고와 신원을 검증한다. config v1 및 공통 reset/cycle/launch는 유지한다.
- 조기 opt-in, 잘못된 인자 fallback 차단, 제품 쓰기 차단, 단일 native 소유자와 정상 callback/Shutdown을 유지했다. 관찰 모드는 공통 초기 Overlay getter도 건너뛴다. 일반 제품 및 별도 smoke/reset/restart는 보존한다.
- 패널은 세 사용자 보고 버튼, 단회 교체, 최초 핵심 오류, 종료로 구성한다. 완료 문구는 “관찰 결과 저장 완료”이며 프로세스 종료나 Steam tracking 해제를 선확인하지 않는다.

## 검증 및 후보 상태

동일 소스 manifest에서 다음 자동 검증의 exit는 모두 0이다.

|검증|결과|
|---|---|
|core|EditMode 254 passed; PlayMode 107 passed / 4 skipped / 0 failed|
|UI|Windows dotnet build 및 EditMode 1,359 passed|
|관찰/startup/reset/restart/platform/save focused full|EditMode 815 passed / 1 skipped; PlayMode 7 passed / 3 skipped; failed 0|
|공통 업적 startup/publication 추가 focused full|EditMode 40 passed; PlayMode 선택 0건(통과 근거로 사용하지 않음)|
|Windows helper fake suite|88 passed; 실제 Steam/game/native API 실행 0|
|삭제 의존성 정적 확인|지정된 관찰 production 파일의 삭제 대상 참조 0|

관찰 전용 flow/evidence-wire/startup fixture는 각각 35/36/11건 통과했다. 처음 잘못 지정한 focused 필터의 0건 실행과 이전 선택 기능 유지안의 결과에는 검증 크레딧을 부여하지 않았다. 실제 선택 건수는 XML과 `focused-filter-counts.json`에 보존했다.

무필터 full은 실행하지 않았다. 위 결과는 선택한 변경 영역의 검증이며 broad/full 회귀 완료를 뜻하지 않는다. 관찰 패널의 batch skip은 렌더링·작은 화면·키보드·마우스·실제 종료 조작 검증이 아니다. 해당 수동 검증과 실제 Steam 표시는 미실행이다.

로컬 후보 `20260909T095216Z-observation-minimal`을 빌드했다. 빌드 exit 0 / 오류 0이며, compiled 검사에서 204개 DLL/EXE hash와 조기 진입·쓰기 차단·report/v2 연결 및 삭제 타입 부재를 확인했다. 최종 257개 payload의 raw→검사→final 무결성을 검증했다.

- 후보: `/mnt/d/J2M/builds/overlay-handoff-observation/20260909T095216Z-observation-minimal/candidate/payload`
- 새 config (Version=1): `/mnt/d/J2M/evidence/overlay-handoff-observation-minimal.json`
- 후보 증거: `/mnt/d/J2M/evidence/overlay-handoff-observation/20260909T095216Z-observation-minimal`
- manifest SHA256: `3b32e962aecd53cdbce6f591976961165171cfc60eac80383de98a47895252fb`
- config SHA256: `97d7e855cd602ab780a673482ae3115578a9926be9ebaff8fb2797b032f02e6d`

임시 define은 정확히 복원됐다. 검증 과정에서 바뀌거나 삭제된 기존 Addressables 생성 파일은 사전 백업에서 복원했으며 빌드 후에도 원래 바이트를 유지한다. 의도한 수정 외 기존 사용자 파일 변경은 0건이다. 테스트 파일명 변경에는 기존 `.meta` GUID를 유지했다.

**구현·자동 검증·로컬 후보 준비 완료이며, 전체 수용 완료는 아니다.** 실제 패널 조작과 Steam 표시 비교는 아직 검증하지 않았다. 현재 설치 전체 payload는 새 후보와 일치하지 않으므로 새 절차를 안내하거나 적용하지 않는다. 관찰 인자를 제거한 일반 실행을 무쓰기 rollback으로 취급하지 않는다.

증거 루트: `/mnt/d/J2M/evidence/overlay-minimal-implementation-20260909T095216Z`.
기존 미커밋 파일은 `before.tar.gz`, `before.diff`, `before-files.json`으로 보존했다.

마지막으로 확인한 설치 manifest의 buildid는 25203840이다. 최소안 후보를 **BuildID 25206354**로 업로드했다(AppID 5218360 / DepotID 5218361). 업로드 전후 257개 payload hash가 일치했고 SteamCMD exit는 0이다. [업로드 기록](/mnt/d/J2M/evidence/overlay-handoff-observation/20260909T095216Z-observation-minimal/upload/upload-summary.md). branch 변경·설치·실제 Steam 표시 시험은 수행하지 않았다. 실제 패널의 작은 화면·키보드·마우스 조작은 batch 시험과 별개이며, batch skip을 시각 통과로 해석하지 않는다.

## 결론의 한계

사용자 보고 비교, 관찰 경계의 금지 쓰기 차단, 검증 시점 사이 참가자 파일 바이트 비교, 검증된 단회 GameOnly 인계만 평가한다. 원격 업적 불변·hook 원인·graphics 동일·중간 모든 파일 쓰기 0·프로세스 종료·Steam tracking 해제는 이 자동 검증의 결론이 아니다. 기존 업적 표시 갱신 시험은 미완료다.

## 2026-09-09 설치 및 실제 단회 관찰 후속

위 후보 준비 시점의 설치 불일치·실제 시험 미실행 상태 이후, Build25206354 설치의 전체 257개 파일 일치와 추가 파일 없음을 확인했다. 실제 run `e301cb80c3994177ad5e1cba3e0f12e3`은 원본·자식 모두 Source=User/AttemptReported=true/opened를 저장했다. v2 보고/context/receipt hash와 process identity 연결, 동일 Steam client, GameCreated 1건 및 helper 완료, 5개 참가자 파일 검증 시점의 집합/hash 일치를 확인했다. 원본 ParentExited와 자식 종료 요청은 기록됐고 사용자가 종료를 보고했다. 자식 실제 종료·Steam tracking 해제를 독립 확인한 것은 아니다.

이는 무초기화 GameOnly에서 Overlay가 보인 1회 사례다. 작은 화면·키보드/마우스 각각·준비 중 종료 등 나머지 수동 수용과 업적 표시 갱신은 완료 처리하지 않는다. 기존 자동 검증이나 코드 상태를 변경하지 않았다.

[실제 run 검토](/mnt/d/J2M/evidence/overlay-handoff-observation/20260909T103944445Z-e301cb80c3994177ad5e1cba3e0f12e3/external-review.json), [후속 업적 시험 구체안](./Achievement-Overlay-Refresh-Next-Trial-Plan.md).
