# Reset / Overlay 조율 시험

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


> 현재 제한된 두 역할 GameOnly 구현은 [Achievement-Overlay-Refresh-Implementation.md](Achievement-Overlay-Refresh-Implementation.md)를 따른다. 아래 FullCycle/final observer 내용은 과거 시험 동작과 근거의 기록이며 새 시험의 실행 요구가 아니다.

2026-09-08. 별도 opt-in 진단 경로이며 제품 CompletedReset 구현 완료를 뜻하지 않는다. 기존 Ctrl+Shift+F10의 GameOnly/Probe/FullCycle은 업적을 초기화하지 않는다. 새 시험은 opt-in 진단 전용 화면에서 기준 상태 확인 후 시작한다. 일반 실행의 참가자 초기화 버튼과 구분한다.

2026-09-09: 사용자 지시로 P1/P2 수정 후보 **BuildID 25190245**를 업로드했다. branch 활성화·새 후보 설치는 수행하지 않았으며, 전용 패널의 실제 렌더링·입력 검증은 미완료다. 아래 절차는 아직 새 후보의 실제 시험 완료나 설치 확인을 뜻하지 않는다. [업로드 기록](/mnt/d/J2M/evidence/participant-restart-preflight/20260908T144501Z-trial-correction/upload-summary.md)과 [수정 검증 기록](/mnt/d/J2M/evidence/participant-restart-preflight/20260908T144501Z-trial-correction/implementation-status.md)을 먼저 확인한다.

## 범위와 선행 조건

- 승인한 AppID 5218360의 시험 계정, 이미 Ready인 참가자 journal, 획득한 매핑 업적 최소 1개가 필요하다. 비교 대상을 만들기 위한 자동 SetAchievement/재획득은 하지 않는다.
- 초기화 범위는 `VQ_LEVEL_0_CLEAR`부터 `VQ_LEVEL_4_CLEAR`까지 5개 Steam 업적 및 기존 참가자 campaign/로컬 업적 데이터다. 전체 Steam 통계나 다른 업적 초기화가 아니다. Steam 초기화와 로컬 초기화는 되돌림을 보장하지 않는다.
- 검증된 Gate A/B prerequisites, 검토된 Gate C 자료와 설치된 전체 payload manifest가 필요하다. 최초 preflight가 설정·prerequisites·Gate C·manifest의 SHA256을 context에 고정하고 후속 단계마다 확인한다. 누락/추가/혼합 설치 파일도 거부한다.
- 빌드에는 `J2M_PARTICIPANT_RESTART_EXPERIMENT`와 `J2M_PARTICIPANT_RESET_DIAGNOSTICS`를 임시 적용하고 원복한다. 설치·계정·초기화 대상을 확인한 뒤에만 아래 설정을 활성화한다.

설정 예시(비활성: SteamId 0은 실행 거부):

```json
{
  "AppId": 5218360,
  "SteamId": 0,
  "PrerequisitesPath": "D:\\J2M\\evidence\\fullcycle-20260908.json",
  "GateCEvidence": "<검토된 Gate C 기록의 D 경로>",
  "PayloadManifestPath": "<현재 설치 빌드의 전체 manifest D 경로>"
}
```

최초 실행 인자는 `-j2mPlatformProvider steam -j2mResetOverlayTrial "D:\J2M\evidence\reset-overlay-config.json"`이다. 기존 `-j2mRestartExperiment`와 혼용하지 않는다. 내부 `-j2mResetOverlayContext`, `-j2mResetOverlayPhase`, `-j2mRestartObservation`은 helper가 생성한 자식에게만 전달한다.

## 한 번의 시험 순서

1. Ready journal이 있는 최초 opt-in 게임은 일반 메뉴 구성을 보류하고 진단 전용 화면을 연다. 제품 업적 서비스·Steam 자동 게시·campaign production 접근은 startup부터 보류한다. Steam 가용성, 설정/계정/Ready/파일을 읽기 전용으로 검증한 뒤 5개 SDK 기준 상태를 화면과 증거에 기록한다. 조회 실패/전부 미획득이면 Pending을 쓰지 않고 중단한다. Overlay 상태를 비교·기록하고 확인란을 선택한 뒤 전용 초기화 버튼을 한 번 누른다. 버튼은 계정/Ready 작업/입력/SDK 기준 상태를 다시 확인하고 변경되었으면 중단한다.
2. 최초 게임은 Pending을 저장하고 GameOnly로 초기화 worker를 한 번 생성한다. Steam 종료 요청은 없다.
3. worker는 일치하는 Pending·계정·파일·소유 인계를 검증한 뒤 기존 coordinator의 ResumeAsync를 한 번 실행한다. Steam 초기화, 로컬 초기화, Ready 저장 후 5개 SDK 미획득과 로컬 비어 있음을 검사한다. 일반 메뉴·업적 서비스·campaign 접근은 계속 보류한다.
4. `AwaitingOverlay` 화면에서 Shift+Tab으로 **Steam 재시작 전** Overlay를 열고 상태를 기록한다. 열리지 않거나 오류가 있으면 종료하고 증거를 검토한다. 기록 확인란과 계속 버튼으로만 다음 단계로 진행한다.
5. worker는 미획득 상태를 다시 조회하고 FullCycle을 한 번 요청한 뒤 종료한다. helper는 기존 Steam 정상 종료→새 Steam→깨끗한 probe 준비→같은 exe 최종 observer 생성을 수행한다.
6. 최종 observer는 일치하는 Ready·소유 인계·계정·로컬 상태·SDK 미획득을 **서비스 시작 전** 검증한다. 이후 일반 서비스를 시작하고 메뉴 구성/재조정 후 SDK를 다시 조회하여 `FinalMenuReady`와 기존 `ChildMenuReady`를 남긴다. 초기화와 재실행 버튼은 제공하지 않는다.
7. 실제 새 메뉴 입력과 Overlay 상태를 기록하고 플레이/재획득 없이 정상 종료한다. 게임 프로세스 종료와 Steam의 실행 중 표시 해제를 따로 확인한다.

최초 준비와 worker/final의 자동 서비스 보류는 초기 composition에서 적용한다. 잘못된 trial 인자나 constructor 실패도 일반 실행으로 되돌리지 않는다. 최초 준비는 일반 메뉴 Ready callback에 의존하지 않는다. 준비 중 종료하면 늦은 continuation으로 다음 단계를 시작하지 않는다.

worker/final은 helper의 원자적 `reset-overlay-child.json` 인계 확인과 Steam 가용성을 하나의 단조 30초 예산에서 기다린다. 최초 게임은 receipt 없이 같은 seam에서 Steam 가용성을 기다린다. receipt는 nonce/op/role/PID/StartTicks를 검증하며 메뉴 준비 증거를 대신하지 않는다. 기존 helper의 준비/cleanup 예산과 cycle lock 계약을 유지한다.

## 기록 및 중단

`D:\J2M\evidence\reset-overlay-trial\<UTC>-<trialId>` 아래 context, 단계별 `*.claimed.json`, `observations-<PID>.jsonl`, `handoff-GameOnly`, `handoff-FullCycle`을 보존한다. snapshot은 5개 이름/조회값을 기록한다. helper의 probe 결과·시도 결과·종료 결과와 최종 자식 기록도 함께 검토한다. trial 기록 실패는 성공을 거부한다. 제품 진단은 기존 best-effort 정책을 유지한다.

모든 trial 역할은 전용 상태 화면을 사용하며 공통 participant 재시작 팝업은 표시하지 않는다. 오류에는 증거 안내와 종료만 제공한다. helper도 request/역할 판별 전 실패를 포함해 Pending·부분 초기화 가능성과 중단을 안내하며 일반 게임 재실행을 권하지 않는다. 이 안내가 사용자의 trial 밖 일반 실행을 기술적으로 차단하는 것은 아니다.

claim은 CreateNew 한 번만 허용한다. 부분 기록·생성 불확실·후속 오류에도 자동 재시도하지 않는다. context/claim은 재개 journal이 아니다. 오류 때 Ready가 남았더라도 전체 시험 성공을 뜻하지 않으며, Pending이 남았다면 일반 게임 재실행이 제품 복구를 진행할 수 있으므로 자료 검토 전 재실행하지 않는다. 시험 인자 제거는 진단 경로 비활성화이며 이미 지운 업적/저장의 복구가 아니다.

## 판정

| 관측 | 판정 |
|---|---|
| 초기화 또는 SDK/로컬 확인 실패 | 해당 단계 중단. FullCycle 실행하지 않음 |
| 중간 SDK 미획득, 중간 Overlay도 미획득 | 재시작 전 갱신 관측 |
| 중간 SDK 미획득/Overlay 획득, 최종 둘 다 미획득 | 재시작 후 표시 갱신 관측. 단독 인과관계 입증은 아님 |
| 최종 SDK 미획득/Overlay 획득 | 표시 불일치 지속 |
| 최종 SDK 획득 | 재게시/초기화 결과를 조사. 성공 아님 |
| 조회·계정·인계·출력·기록 누락 | 증거 부족. 성공 추정 금지 |

fake 검증과 실제 UI/SDK/Overlay 검증을 구분한다. 제품 두 게임 교체·canonical Ready·완료 retry·Editor Domain Reload·KO/EN UI 완성은 별도 제품 작업이다. 이번 결과로 원래 native fatal 원인이 완전히 규명됐다고 주장하지 않는다.
