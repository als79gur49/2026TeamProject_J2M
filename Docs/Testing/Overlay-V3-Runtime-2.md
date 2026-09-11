# Overlay v3 수동 실행

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


런처와 외부 설정 파일 입력을 제거했다. 이 문서는 현재 단순화된 소스 기준이며, 이전 업로드에 적용됐다는 뜻은 아니다.

## 실행

Steam의 Vector Quake 사용자 시작 옵션은 비운다. 시작 옵션을 저장하면 FullCycle의 replacement 실행에도 Origin 인자가 붙기 때문이다.
Windows `Win+R`에서 다음을 실행한다. 별도 cmd/PowerShell 스크립트는 필요 없다.

```text
"C:\Program Files (x86)\Steam\steam.exe" -applaunch 5218360 -j2mOverlayV3Role OriginObserver -j2mOverlayV3Owner SteamDelegated
```

Steam의 기존 고정 실행 항목이 `-j2mPlatformProvider steam`을 전달한다. 위 명령에 다시 추가하지 않는다. `-j2mOverlayV3Config`와 revision 문자열은 실행 인자로 넣지 않는다.

게임은 현재 실행 파일/Steam 경로와 로컬 `Saves/exhibition-reset.json`을 읽는다. Ready journal의 계정과 실제 native 계정을 대조한다. 결과는 `D:\J2M\evidence\overlay-v3-runs\<UTC>-<run>`에 자동 기록한다. 이 폴더는 실행 전에 준비하는 입력이 아니다. journal을 생성·reset·복구하거나 참가자를 변경하지 않는다.

## 게임에서 확인

1. 관찰 입력 가능 상태에서 Shift+Tab으로 Overlay를 열고 실제로 보이는지 확인한다.
2. 게임으로 돌아와 관찰 내용을 입력하고 표시 여부를 저장한다.
3. 대상별 획득/미획득/판정 불가를 선택해 저장한다. 기록 시각은 자동 저장된다. 미획득 보고 후에도 창을 유지한다.
4. SDK baseline이 모두 미획득이고 Overlay가 열린 상태에서 대상별 획득/미획득 보고를 저장하면 단회 Steam 재시작 버튼을 사용할 수 있다. 0/5 및 획득·미획득 혼합 보고도 허용하며, 판정 불가가 남으면 진행하지 않는다.
5. 재시작 버튼을 한 번 누른 뒤 직접 Steam/게임을 실행하지 않고 자동 재실행을 기다린다. 새 게임에서 Overlay 열림과 대상별 표시를 보고한다. 시작부터 0/5였다면 자동 경로 및 미획득 유지 확인이며, 잔존 표시 제거 성공으로 해석하지 않는다.
6. 종료 버튼 또는 창 닫기로 종료한다. 로컬 관찰 오류는 첫 오류를 화면과 Player.log에 남기고 native 사용을 종료한다. 오류 화면은 사용자가 닫는다.

## 유지하는 계약과 제거한 조건

- canonical native Init/Update/Shutdown 단일 소유권, 계정/App/health 확인, reset·재획득·참가자 쓰기 금지 유지.
- 같은 journal bytes의 typed 값/hash 대조 유지. 관찰 중에는 설정과 journal만 재확인한다. 전체 설치/저장 폴더 inventory는 검사하지 않는다.
- SDK inventory, Build/Depot/Manifest, Gate A/B, 과거 argv/cwd Verified 문서, 외부 config 그래프는 실행 조건에서 삭제했다. 단회 SDK baseline은 획득 값을 그대로 기록한다.
- FullCycle은 마지막 느린 확인 뒤 admission commit, generation/deadline, 단회 grant, 단일 Close, pending report 실패를 유지한다. 실제 사용할 exe/native DLL은 인계 경계에서 확인한다.
- WMI process-start watcher와 WMI argv 조회는 삭제했다. 실제 pipe peer PID/retained handle과 run/context/request 귀속은 확인한다. argv/cwd는 연결된 child의 보고를 대조하며 독립적인 OS 조회 증거로 표시하지 않는다. 미연결·단명 추가 프로세스 전체 검출이나 계속적인 Steam singleton 감시는 제공하지 않는다.
- replacement 정상 완료는 보고 ack, native Shutdown 1회 정상 반환, lifecycle/exit-completed와 실제 child 종료가 모두 필요하다. EOF만으로 성공하지 않는다.
- `Version=3`, `ProtocolRevision=observation-v3-runtime-2` 유지. DTO를 축소하여 이전의 추가 필드가 있는 config/request는 strict reader가 거부한다. 기존 evidence를 변환/덮어쓰지 않고 새 실행에서 새 문서를 만든다. legacy reset journal은 schema 1 그대로다.

## 검증 범위

관련 fixture, `./run_tests.sh core`, `./run_tests.sh ui`, Windows harmless multiprocess/pipe fake로 검증한다. fake는 실제 Steam/native 상태의 증거가 아니다. 화면은 고정된 대상 행과 항상 존재하는 오류 label을 사용해 준비/오류 전환 중에도 컨트롤 수가 바뀌지 않는다. batch graphical 시도에서는 OnGUI 이벤트가 발생하지 않아 실제 화면 렌더링은 검증하지 못했다. 구형 화면 테스트나 UI 통과 건수로 이를 대체하지 않는다. 실제 설치본 Overlay 관찰 결과 역시 자동 테스트와 구분한다.
