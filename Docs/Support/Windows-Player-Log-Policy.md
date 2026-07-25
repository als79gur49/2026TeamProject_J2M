# Windows Player.log 운영 및 지원 정책

## 출시 계약

- Store configuration ID: `windows-x64-store-mono-logon-v1`
- Windows 초기 출시: `Player.log` ON
- 로그 정책 ID: `local-player-log-no-auto-upload-v1`
- 자동 업로드: 없음
- 사용자가 명시적으로 제출하는 support log: 허용

이 정책은 로컬 로그 생성과 원격 전송을 분리한다. 게임, project-owned
runtime code, Unity Cloud Diagnostics, Unity Analytics 또는 제3자 crash
reporter가 전체 `Player.log`나 custom telemetry를 백그라운드에서
자동 전송하지 않는다. 새 telemetry 서비스 도입은 별도 제품·privacy
결정 없이는 허용되지 않는다.

## 기본 위치와 rotation

ProjectSettings의 Company는 `J2M`, Product는 `VectorQuake`다. Windows 기본
위치는 다음과 같다.

```text
%USERPROFILE%\AppData\LocalLow\J2M\VectorQuake\Player.log
%USERPROFILE%\AppData\LocalLow\J2M\VectorQuake\Player-prev.log
```

`Player.log`는 현재 실행 로그이고, Unity가 다음 실행을 시작할 때 기존
로그를 `Player-prev.log`로 회전할 수 있다. 실제 release smoke마다 두
파일의 생성·회전 결과를 기록한다.

## 지원 요청 제출

문제 재현 직후 다음 정보를 지정된 private support channel로 제출한다.

- `Player.log`
- 필요하면 `Player-prev.log`
- 게임 version
- 문제가 발생한 현지 시각과 timezone
- 재현 단계

공개 게시판, 공개 issue 또는 채팅방에 raw 로그를 게시하지 않는 것을
권장한다. 지원 담당자가 요구하지 않은 save 원문, PlayerPrefs/registry
dump 또는 credential은 첨부하지 않는다.

## Privacy와 보관

Unity engine 로그에는 OS/GPU/driver/Unity version, 장치 정보, 로컬 사용자
경로와 native stack이 포함될 수 있다. 제출 전에 사용자가 내용을
검토하거나 경로·식별 정보를 redaction할 수 있다.

Project-owned production log에는 credential, access token, password,
email, 실명, Steam/account ID, 전체 save document를 남기지 않는다.
지원 로그는 Store payload와 공개 build evidence에 포함하지 않고 private
QA/support root에만 보관한다.

지원 조직은 별도의 보관 기간, 접근 권한과 삭제 절차를 정해야 한다.
현재 repository는 원격 수집이나 장기 보관을 구현하지 않는다.

## 안정화 이후 재검토

`Player.log` OFF 전환은 Windows 출시 안정화와 대체 crash diagnostics를
확보한 뒤 별도 milestone에서 재검토한다. 이번 정책은 Unity Cloud
Diagnostics나 자동 telemetry를 새로 도입하지 않는다.
