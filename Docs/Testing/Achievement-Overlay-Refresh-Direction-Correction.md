# 업적 Overlay 표시 갱신: 진행 방향 정정

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


2026-09-09. 사용자 문제제기와 서브 에이전트 `trial_purpose_review`의 읽기 전용 재검토를 반영한다. **후속 질문과 시험 우선순위는 이 문서를 따른다.** 이전 계획·구현·실행 기록은 이력으로 보존한다. 이 문서는 새로운 구현, 업로드, Steam 재시작, 게임 실행 또는 업적 변경의 승인이 아니다.

## 1. 유지할 목적

목적은 참가자 초기화 후 업적 표시를 갱신하면서, 다시 실행한 게임에서 Overlay도 사용할 수 있게 하는 것이다. GameOnly 초기화 비교를 반복하는 것이 목적이 아니다.

다음 검토 질문은 **“이미 초기화된 상태에서, 수동으로 성공했던 Steam 완전 재시작·게임 시작과 자동 helper 경로는 무엇이 다르며, 자동 경로에서 Overlay 열림과 표시 갱신을 함께 확보할 수 있는가?”**다.

## 2. 이미 확보한 근거

| 시점·근거 | 관찰 및 한계 |
|---|---|
| 9월 7일 SDK/화면 비교 | 초기화 readback·서비스 시작 전·메뉴 준비 후 SDK 미획득과 이후 Overlay 획득 표시 잔존이 이미 기록됐다. 사용자 보고로 Steam client 화면의 지연 갱신도 있었다. 동일 순간의 관측이나 내부 캐시 직접 관측은 아니다. |
| 9월 7일 사용자 수동 절차 | 게임 단독 재시작·업적 창 재개방·Overlay 데이터 삭제는 효과가 없었고, 게임 종료 → Steam 완전 종료 → Steam 재실행 → 게임 시작 후 Overlay 0/5를 확인했다. 수동 성공 사례이며 자동 helper 성공이나 매회 갱신 보장은 아니다. |
| 9월 9일 `428682c9…` | GameOnly worker에서 SDK 5개 미획득·로컬 초기화 뒤 Overlay 자체가 열리지 않아 FullCycle 전에 중단했다. 이는 ‘열리지만 획득 표시가 남음’과 다른 증상이다. |
| 최소 observation `e301cb80…` | 초기화 없이 동일 client의 GameOnly 교체 후 원본·자식 모두 Overlay opened로 보고했다. 교체만으로 항상 Overlay가 실패한다는 가정은 지지하지 않는다. 업적 표시 갱신 결과는 아니다. |
| 최종 reset 비교 `4956de62…` | 초기 SDK 및 사용자 화면에서 `VQ_LEVEL_0_CLEAR` 획득. 단회 GameOnly worker에서 SDK 5개 미획득·로컬 초기화 검증·Ready 완료 후, 사용자는 Overlay opened 및 같은 대상 earned를 보고했다. baseline·결과·보고 hash 연결을 확인했다. |

근거 원문:

- [9월 7일 SDK와 Overlay 재검토](/mnt/d/J2M/evidence/overlay-recheck-20260907T140949Z/README.md)
- [수동 Steam 재시작 성공을 반영한 기존 구현안](/mnt/d/J2M/evidence/steam-client-restart-design-20260907/implementation-plan.md)
- [GameOnly worker Overlay 불능 조사](/mnt/d/J2M/evidence/reset-overlay-trial/20260908T161149470Z-428682c9ff2449d3a3c2e83ed3b9f0db/overlay-unavailable-review/investigation.md)
- [최소 observation 결과](Overlay-Handoff-Observation-Minimal-Implementation.md)
- [최종 SDK baseline](/mnt/d/J2M/evidence/reset-overlay-trial/20260909T135413795Z-4956de62dc69455fbf670e627feb9c7c/sdk-baseline.json), [초기 화면 보고](/mnt/d/J2M/evidence/reset-overlay-trial/20260909T135413795Z-4956de62dc69455fbf670e627feb9c7c/Initiator/user-report.json)
- [worker 초기화 결과](/mnt/d/J2M/evidence/reset-overlay-trial/20260909T135413795Z-4956de62dc69455fbf670e627feb9c7c/ResetWorker/reset-result.json), [최종 화면 보고](/mnt/d/J2M/evidence/reset-overlay-trial/20260909T135413795Z-4956de62dc69455fbf670e627feb9c7c/ResetWorker/user-report.json)

사용자는 과거 자동 Steam 재시작 뒤 Overlay 불능도 보고했다고 회고했다. 이번 재검토에서 특정한 `428…` 실행은 GameOnly 단계에서 중단한 사례이므로 그 회고와 같은 실행으로 합치지 않는다. 자동 재시작 불능의 정확한 실행·시점은 기존 자료에서 별도로 대응시킬 항목이다. 자료 대응이 부족하다는 이유로 사용자 관찰 자체를 없었던 사실로 취급하지 않는다.

## 3. 이번 시험의 의미와 계획 선택의 잘못

GameOnly는 게임 교체 방식이다. 업적 초기화는 worker의 기존 coordinator가 수행한다. 따라서 이번 시험은 GameOnly 자체의 초기화 능력을 시험한 것이 아니다.

Overlay 자체 불능을 초기화와 분리하기 위한 무초기화 observation은 의미가 있었다. 그러나 그것이 열린 다음, [후속 계획](Achievement-Overlay-Refresh-Next-Trial-Plan.md)은 다시 GameOnly reset 표시 비교를 우선하고 Steam 재시작 검토를 ‘여전히 필요할 때’로 미뤘다. 이미 있던 게임 단독 재시작 무효·수동 Steam 재시작 성공 보고를 재검증해야 할 구체적 차이와 필요성을 충분히 제시하지 못했다.

이번 시험은 현재 후보에서 자동 게시 보류, 동일 client, 단회 인계, baseline·사용자 보고·초기화 결과의 연결을 강화해 **기존 표시 불일치를 재현한 사례**다. ‘GameOnly만으로 부족함을 새로 발견했다’, ‘SDK 초기화 실패와 화면 불일치를 처음 구분했다’는 설명은 정정한다. 안전한 구현과 기록 보강의 가치는 있지만, 원래 문제에 대한 다음 시험으로 선택할 타당성과는 별개다. 기존 맥락을 유지하지 못한 계획 선택과 결과 설명의 잘못이다.

## 4. 다음 검토의 순서와 중단 기준

1. 기존 수동 성공 및 자동 실패 기록을 실행별로 대응시킨다. 게임만 교체한 실행과 Steam까지 재시작한 실행, Overlay 불능과 업적 표시 잔존을 분리한다. 과거 Gate B/C의 프로세스·메뉴 성공을 Overlay 성공으로 대신하지 않는다.
2. 소스와 기존 기록을 읽어 수동 성공 절차와 자동 helper의 종료·재시작·게임 시작 경로를 비교한다. 현재 자동 GameOnly/FullCycle은 같은 exe를 `Process.Start`로 생성하며 FullCycle은 Steam 재시작·probe를 앞에 수행한다. Steam이 게임 실행을 맡는 경로와의 차이는 검토 대상이지 확정 원인이 아니다.
3. 추가 구현이나 실제 시험을 제안하기 전에, 기존 자료로 답할 수 없는 질문, 비교할 단일 차이, 예상 판정과 그 판정이 다음 선택을 어떻게 바꾸는지 설명한다. 기록을 더 정교하게 만든다는 이유만으로 새 초기화를 요구하지 않는다.
4. 실제 후속 시험이 필요하면 재획득·재삭제 없이 이미 초기화된 상태의 표시를 관찰하는 방향을 우선한다. 마지막 run의 미획득/Ready는 당시 기록이다. 미래 실행 시점의 live 조건을 대신하지 않으며, 이미 표시가 갱신되어 비교 조건이 사라졌다면 조건 미충족으로 판단한다.
5. 기존 FullCycle 버튼·소비한 context/claim을 재개하지 않는다. 제품 서비스 재개에 따른 재게시 가능성, 소유 인계·client 신원·실행 파일·결과 저장 조건은 후보의 실제 연결을 검토한 뒤 정한다. 이번 문서만으로 실행하지 않는다.

새 근거 없이 GameOnly reset 비교 반복, 비교 대상 생성을 위한 재획득/SetAchievement, Clear/Store 반복, 계정 교체를 다음 기본 단계로 삼지 않는다. 최소 observation의 무쓰기 계약과 기존 삭제 기능의 비범위를 유지한다. 새 collector·환경/DX/Overlay 설정 변경·Computer Use를 자동으로 추가하지 않는다.

## 5. 결론을 표현하는 기준

- 현재까지의 결론: 게임 단독 재시작의 표시 갱신 실패와 수동 Steam 완전 재시작 성공이 과거에 보고됐고, 최신 제한 시험에서도 SDK 초기화 후 Overlay 획득 표시 잔존을 재현했다.
- 아직 해결하지 못한 것: 수동 성공 절차를 자동 경로에서 재현하여 Overlay 열림과 미획득 표시를 함께 확보하는 것.
- 아직 단정하지 않을 것: 특정 캐시/환경/graphics가 원인, Steam 재시작이 모든 경우 필수, GameOnly가 모든 경우 실패, 이후 지연 갱신 없음.
- 종료 요청·HelperCompleted는 자식 실제 종료 또는 Steam tracking 해제의 증명이 아니다. SDK 관측은 원격 서버 상태의 독립 증명이 아니다.

후속 계획은 이 다섯 절과 충돌하는 가정을 도입할 때 그 이유와 새 근거를 명시해야 한다. 이전 계획의 상세 체크리스트를 수행했다는 사실만으로 진행 방향이 타당하다고 판단하지 않는다.
