# Campaign 저장 오류 — 메뉴 복귀 구체 수정안

- 작성일: 2026-09-25
- 상태: **2026-09-25 runtime 수정 적용, 자동 검증 실행 완료. 번역 적용·Player 확인 미완료**. 아래 내용은 구현 기준이며 최종 실행 결과는 [실행 보고서](./Campaign-Casual-Hardcore-Implementation-Report.md)를 따른다.
- 대상: 현재 worktree의 Casual/Hardcore 초기 구현. 별도 commit·push·PR은 포함하지 않는다.
- 확정 방향: 저장 오류 시 현재 실행 중지 → 메뉴/종료 안내. 진행 중 슬롯은 기존 Continue, 완료 슬롯은 기존 완료 카드로 처리한다.
- 이 문서는 [모드 설계](./Campaign-Casual-Hardcore-Mode-Design.md)의 이전 전용 Reload/완료 화면 복구안을 대체한다. 정상 성공 시 GameClear·outro는 유지한다.
- 검토 근거: UI·저장·Gameplay 서브 에이전트의 `/mnt/d/J2M/evidence/campaign-modes-implementation/confirmed-menu-direction-review-20260925.md`.
- 구체안 재검토: 2026-09-25 세 서브 에이전트가 실제 코드와 대조했다. 기본 버튼 선택·popup 닫힘 통지·backup 통지 전 mutation 차단·로컬 Iris 실패 처리의 보완을 아래에 반영했다. runtime 적용 증거는 아니다.
- 재검토 기록: `/mnt/d/J2M/evidence/campaign-modes-implementation/concrete-menu-plan-review-20260925.md`.

## 1. 사용자에게 보이는 흐름

```text
HP 변화 / 사망 / 클리어
  ├─ 저장 성공 확인 → 기존 정상 연출·다음 단계
  └─ 저장 실패 / 저장 여부 불명 / 실행 중 backup 복구 감지
       → 현재 플레이 중지, 이전 결과는 다시 저장하지 않음
       → 오류 팝업: [메인 메뉴] [종료]
            ├─ 종료 → 앱 종료
            └─ 메인 메뉴 → 기존 파일 읽기·복구·검증
                 ├─ 진행 중 슬롯 → 사용자가 Continue → 저장 상태로 스테이지 시작
                 ├─ 완료 슬롯 → 완료 카드 표시
                 └─ 읽기 실패·미지원 → 기존 저장 오류 UI
```

여기서 Continue는 기존 메뉴 버튼이다. 오류 팝업에 별도 Retry/Reload/ManualRetry를 만들지 않는다. 메뉴에 도착했다고 자동 플레이하거나 슬롯을 초기화하지 않는다. Continue 시 스테이지의 보드는 처음부터 시작하며 HP·Chance·목적지는 파일에 저장된 값을 사용한다. 중간 보드 snapshot을 복원하는 기능은 없다.

완료 카드를 표시하는 것만으로 복구가 끝난다. 완료 화면·엔딩·코믹을 오류 복구 목적으로 재생하지 않는다. 완료 카드의 Restart는 기존처럼 모드 선택과 명시적 확인을 거쳐 새 게임을 만드는 별도 행동이다.

## 2. 수정할 코드와 이유

아래 경로는 `Assets/_Features/` 기준이다. 추가 API 이름은 제안이며, 동일 책임의 기존 API가 있으면 그것을 사용한다.

| 파일/영역 | 구체 수정 | 이유 |
| --- | --- | --- |
| `Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs` | `ReloadCampaignSave` 구현, gateway Reload 바인딩/해제를 삭제. 실패 시 실행 폐기·token 정리·실패 알림만 수행 | 현재 전용 Reload의 handoff/source가 production router의 허용 계약과 맞지 않는다. 기존 메뉴 Continue로 통합 |
| `Gameplay/Gameplay_UIAccess/Runtime/Contracts/IGameplayCommandGateway.cs` | `CampaignReloadResult`, `IGameplayCampaignRecoveryCommands` 삭제 | 사용하지 않는 복구 전용 gameplay 명령 제거 |
| `Gameplay/Gameplay_Host/Runtime/UIAccess/GameplayHostCommandGateway.cs` | 복구 interface, delegate, forwarding 제거 | command gateway를 기존 플레이 명령 책임으로 유지 |
| `UI/UI_Application/Runtime/CampaignSaveFailurePresenter.cs` | Reload 의존성과 `showCompleted` callback 제거. 메뉴/종료 action만 주입. 중복 선택·dispose 이후 callback 무시 | 저장 접근이나 완료 화면 판단을 오류 presenter에서 제거 |
| `UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs` | failure source만 요구하여 presenter 구성. 오류 경로에 기존 `ConfiguredMainMenuReturnRouter`와 `IApplicationQuitPort` 연결 | 기존 메뉴 route와 종료 구현 재사용. 일반 comic outro wrapper를 오류 탈출 경로에서 호출하지 않음 |
| `UI/UI_Composition/Runtime/ConfirmPopupPortAdapter.cs` | 기존 Confirm popup을 사용하는 좁은 저장 오류 dialog port 추가 | 실제 버튼 클릭과 화면 이동에 따른 popup 닫힘을 구분해야 함 |
| 기존 terminal authority (`TerminalTransitionTypes.cs`) | 정확히 일치하는 token의 `Claimed`, transition ID 0 상태만 종료하는 좁은 취소 연산 추가 | 기존 `TryAbortIrisSetup`은 Claimed를 해제하지 못한다. 씬 변경만으로도 남은 token이 자동 해제되지 않음 |
| `Stages/Runtime/Campaign/Save/CampaignSaveService.cs` | death/clear/survival의 load 직후 `BackupRecovered`이면 전이를 적용하지 않고 실패 반환 | 복구된 파일에 이전 월드의 결과를 적용하는 것을 차단 |
| `Stages/Runtime/Campaign/Save/CampaignHudReadStore.cs` 및 Flow 연결 | 활성 실행 중 같은 root의 읽기에서 복구가 관측되면 Flow에 통지. Flow는 실행 폐기. 알림은 gate 밖에서 전달 | HUD/Flow의 선행 읽기가 복구하고 이후 service 읽기는 Loaded가 되는 경우도 검출 |
| `CampaignSaveSlotStoreAdapter.cs` 및 Flow의 결과 계산 전 읽기 | 읽기에 부속된 load report 확인, 읽기 후 abandoned 여부 재검사 | 이미 복구된 슬롯을 읽고 곧바로 PlanDeath/clear를 계산하는 경로 차단 |

`MainMenuController`, 슬롯 평가, 완료 카드, 정상 Continue handoff는 기존 동작을 사용한다. 이 수정 때문에 새 source allowlist, `ManualRetry` 전환, 별도 launch 종류를 추가하지 않는다. profile schema 3 / local-state schema 1, 일반 Save / 생성·삭제의 SaveDestructive 구분도 변경하지 않는다.

### 2.1 오류 팝업의 정확한 동작

기존 `IConfirmPopupPort`는 결과를 bool로 줄여서 일반 닫힘도 false로 전달한다. 단순히 false를 Quit에 연결하면 화면 이동으로 popup이 닫힐 때 앱이 종료될 수 있다.

따라서 application에는 `ICampaignSaveFailureDialogPort.Request(payload, completion)`처럼 작은 port를 두고, 결과를 `Menu / Quit / Dismissed`로 구분한다. composition의 기존 adapter에서 실제 completion kind와 close reason을 검사한다. application이 UI.Flow의 상세 popup 타입을 직접 참조하지 않는다.

- `UserAction + Confirmed` → 종료. `IsConfirmDestructive=true`로 기존 종료 강조 스타일 사용.
- `UserAction + Cancelled` → 메뉴. 기존 ConfirmPopupView는 Cancel 버튼을 기본 선택하므로 Enter/패드 Submit의 기본 동작도 메뉴가 된다. locale 변경 후 선택 초기화도 동일하다.
- 그 밖의 닫힘 → Dismissed. presenter의 표시 중 상태만 해제하고 메뉴/종료 명령은 실행하지 않음. Dispose는 기존 popup callback을 생략하므로 presenter 자체 Dispose로 끝낸다.
- Back은 소비하고 backdrop으로 닫히지 않도록 기존 `ConsumeBack=true`, `SecondaryIsAlternative=false` 정책 사용.
- 한 popup의 callback은 한 번만 처리한다. scene 소유권이 넘어가면 늦은 callback은 무시한다.
- 메뉴 route가 즉시 거절되거나 예외를 반환하면 플레이 차단을 유지하고 메뉴/종료 안내를 다시 표시한다. 저장 명령은 호출하지 않는다.
- popup Push 예외에서는 presenter의 표시 중 상태를 해제한다. 현재 Push는 true 또는 예외이므로 별도 거절 protocol을 만들지 않는다.

일반 Confirm API나 popup prefab은 바꾸지 않는다. 새 popup framework도 만들지 않는다. terminal claim 때문에 키보드·패드 탐색이 막히지 않는지는 token 정리 후 실제 composition 테스트로 확인한다.

### 2.2 실행 중지와 token 정리

순서는 **실행 폐기 latch 설정 → 이번 실행의 소유권 정리 → 오류 알림**이다. 실패 안내 subscriber가 예외를 던져도 플레이를 재개하지 않는다. 일반 pause 해제는 latch를 해제하지 않으며 새 InputHost 초기화만 새 실행을 허용한다.

| 실패 시 상태 | 처리 |
| --- | --- |
| 생존 피격, terminal token 없음 | tick/input/forced clear 차단 후 오류 안내 |
| 사망·클리어 저장 중, token은 Claimed이고 transition ID 0 | 일치하는 token만 취소하여 inactive로 만든 후 오류 안내 |
| 저장 성공 뒤 로컬 Iris 준비 실패, phase Iris이고 transition ID 0 | 기존 `AbortFailedSetup`의 정리 순서를 재사용하여 해당 playback의 event 연결 해제 → registry 해제/Dispose/Hide → 해당 token의 Iris 취소. 늦은 BlackReached는 abandoned/disposed 검사 |
| 이미 Black/WaitingSameSceneDestination, coordinator가 transition ID를 소유하거나 FailedHoldingCover | 같은 transition ID 0이어도 로컬 Iris 준비 실패와 구분. 기존 abort 허용 범위를 넓히거나 다른 owner를 강제로 초기화하지 않음. 기존 늦은 presentation/transition 실패 처리 범위 |

일반 HP/death/clear 저장은 Iris 시작 전에 수행되므로 저장 오류의 핵심 대상은 위 첫 두 행이다. 마지막 행은 기존 늦은 callback/scene 이동 실패 문제로, 메뉴 복귀안만으로 해결됐다고 주장하지 않는다. cover가 남은 상태의 오류 표시·종료 가용성은 별도 확인 대상이다. 이를 해결하는 전체 bootstrap rollback은 이번 수정의 선행 조건으로 추가하지 않는다.

Flow의 기존 Iris setup catch 세 곳은 정리 뒤 stage Launch, LevelFailed publish, clear gate release를 수행하고 다시 throw한다. 오류 메뉴 정책을 적용하는 로컬 setup 실패에서는 이 fallback 후 rethrow를 제거하고 정리 후 Abandon을 한 번만 수행한다. 기존 fallback을 먼저 실행하면 outer catch에서 실행을 폐기할 때 이미 다른 route/화면이 시작될 수 있다. 정상 성공 경로는 유지하고, 이 실패 시 fallback을 기대하는 기존 테스트는 의도적으로 바꾼다.

playback을 단순히 Cancel/Dispose하는 것으로 정리를 대체하지 않는다. Cancelled handler가 먼저 `TryFail`을 호출하면 phase가 FailedHoldingCover로 바뀌어 기존 Iris 취소가 거절된다. 위 순서와 matching token/playback 검증을 따른다. BlackReached 내부에서 새로 발생하는 observer 예외는 outer 동기 catch가 잡지 않으므로, abandoned 검사만으로 해결됐다고 주장하지 않는다.

저장 성공을 기록한 뒤 observer나 route가 실패한 경우에도 저장 성공 표시는 유지한다. 같은 결과를 다시 commit하거나 월드를 rollback하지 않는다.

### 2.2.1 방안 A — 동기 알림 중 복구 보완 (2026-09-25 적용)

사용자가 방안 A를 선택했다. 진단 UI의 파일 재조회 정책을 바꾸는 방안 B는 적용하지 않는다.

- claim owner는 기존 authority가 발급한 token을 Changed 전달 전에 기록한다. 내부 handoff는 token 보관만 수행하며 새 ID를 만들지 않는다. claim 알림 예외도 기존 Flow 실패 처리로 전달한다.
- 복구 감지 시 실행 폐기 latch를 즉시 설정한다. 이미 소유한 로컬 Iris는 즉시 취소하여 Show 전에 멈출 수 있다. Claimed 정리와 오류 안내는 원래 claim/terminal handler의 동기 호출이 끝난 finally에서 마무리한다. 중지 상태와 정리·안내 완료 여부는 구분한다.
- destination Changed 반환 뒤 clear commit 직전에 disposed/abandoned를 검사한다. 죽음 연출·Iris·route 시작 앞에도 중지 여부를 확인한다. feed는 claim 중 실패한 결과로 pending clear를 만들거나 강제 clear 성공을 반환하지 않는다.
- Iris candidate는 phase Changed 전에 port의 로컬 소유로 등록한다. phase 알림은 기존 setup try/catch 안에서 수행하고, 반환 뒤 같은 candidate·token·phase가 유효한지 검사한다. 취소됐으면 Show와 registry 등록을 하지 않는다.
- scope는 현재 동기 호출에 한정된다. 새 queue, 영속 receipt/revision, recovery generation, 전용 Reload 또는 저장 재시도는 추가하지 않는다. coordinator가 이미 소유한 전환의 취소 범위도 확대하지 않는다.

기존 코드에서 신규 테스트 8개가 실패하는 것을 확인한 뒤 수정했다. 실행 결과는 [실행 보고서](./Campaign-Casual-Hardcore-Implementation-Report.md)의 방안 A 항목을 따른다. 이 자동 검증은 실제 Player 메뉴 이동의 완료 증거는 아니다.

### 2.3 backup 복구 처리

`BackupRecovered`는 메뉴에서는 유효한 복구 결과지만, 이미 실행 중인 월드에서는 저장 기준이 바뀌었다는 뜻이다. HP·stage 값이 우연히 같아도 현재 실행의 결과를 복구된 파일에 이어서 적용하지 않는다.

1. service의 death/clear/survival mutation은 자체 load report가 `BackupRecovered`이면 전이·persist 전에 거절한다. 기존 실패 result에 해당 load status를 보존한다. 예를 들어 기존 `StalePrecondition`과 load status를 사용하며 별도 영속 상태는 추가하지 않는다.
2. Flow/HUD 등 선행 읽기의 복구도 기존 root별 read store에서 관측한다. 복구 관측 사실을 메모리에 보관하여 이후 Loaded 보고에 지워지지 않게 하고, 최외곽 operation과 mutation이 모두 끝난 후 gate 밖에서 활성 Flow에 통지한다. 이 메모리 표시는 통지 완료까지 유지한다.
3. death/clear/survival의 admission은 같은 gate 안에서 **이번 load의 BackupRecovered 또는 아직 통지 중인 복구 관측**을 검사하고 전이 전에 거절한다. 자체 load report 검사만으로는 선행 읽기가 이미 복구한 뒤 Loaded를 반환하는 틈새를 막지 못한다. Flow도 결과 계산용 읽기 직후 report와 abandoned를 검사하여 Plan/Commit 진행을 중단한다.
4. Flow는 자신의 활성 수명 동안만 구독하고 Dispose에서 해제한다. root가 다른 DirectPlay/격리 테스트에 영향을 주지 않는다. observer 예외는 저장 재실행을 유발하지 않는다.
5. 메뉴와 새 launch는 복구된 유효 파일을 읽을 수 있다. `BackupRecovered`를 전역적인 접근 차단 상태로 바꾸지 않는다.

이 알림은 기존 동기 gate가 끝날 때 전달하는 일회성 메모리 관측이다. 최외곽 scope 종료 때 gate 안에서 callback snapshot을 한 번 인수하여 중복 dispatch를 막고, gate 밖에서 handler를 각각 호출한다. handler 예외가 있어도 다른 활성 Flow의 차단 통지를 수행하며, finally에서 gate를 잡아 관측 flag를 정리한다. gameplay mutation 차단은 통지 완료 전까지 적용하며 메뉴 읽기·PrepareContinue를 전역 차단하지 않는다. 새 recovery generation, 영속 revision/receipt, 명령 queue, 장기 pending owner를 추가하지 않는다.

복구 알림 생성은 operation 안의 실제 service load 관측(`ObserveProfile`) 한 경계로 좁힌다. HUD의 `Session.Read`는 `_validate()` 이후 같은 report를 `Observe`로 다시 cache에 반영하므로, generic `Observe`에서 새 복구 알림을 생성하지 않는다. gate 해제부터 통지 완료까지의 observer 재진입·같은 root의 경쟁 호출, 선행 읽기 복구, cache 재반영 후 flag가 남지 않는지는 필수 테스트 대상이다.

## 3. 각 case의 파일 상태와 다음 플레이

아래 표의 Continue는 오류 팝업의 메뉴 선택 후 **사용자가 기존 슬롯 Continue를 선택하는 단계**다. 파일 상태는 다음 읽기·검증 결과가 최종 기준이며, 파일을 읽기 전 오류 popup에서는 저장 성공 여부를 추정하지 않는다. before/after 비교는 추가 backup 복구가 없는 경우다.

| 사건 | 쓰기 전에 실패한 경우 | 저장됐지만 응답만 실패한 경우 |
| --- | --- | --- |
| 캐주얼 생존 피격 HP 3→2 | 파일 HP 3. Continue 시 같은 stage, HP 3 | 파일 HP 2. Continue 시 같은 stage, HP 2 |
| 캐주얼 사망 | 파일의 이전 stage·HP·TotalDeaths 유지. Continue는 그 상태 | 파일은 현재 레벨 묶음 첫 stage·HP 3, TotalDeaths +1. Continue는 그 목적지 |
| 하드코어 Chance 3에서 사망 | 파일 Chance 3, 기존 stage | 파일 Chance 2, 같은 stage, TotalDeaths +1 |
| 하드코어 Chance 2에서 사망 | 파일 Chance 2, 기존 stage | 파일 Chance 1, 같은 stage, TotalDeaths +1 |
| 하드코어 Chance 1에서 사망 | 파일 Chance 1, 기존 stage | 파일은 sequence 전체 첫 stage·Chance 3, TotalDeaths +1 |
| 최종이 아닌 stage 클리어 | 파일은 이전 stage. 그 stage를 다시 시작 | 파일의 다음 stage 시작. 캐주얼 HP 3. 하드코어 같은 레벨은 Chance 유지, 다음 레벨은 3 |
| 캠페인 최종 클리어 | 파일은 아직 진행 중. Continue하면 최종 stage를 다시 시작 | 파일은 완료. 메뉴에 완료 카드 표시. Continue·엔딩 복구·자동 Restart 없음 |

사망이 저장된 경우의 TotalDeaths 증가는 그 사건에 대해 한 번이다. 응답 실패 후 이전 사망 명령을 재실행하여 추가 증가시키지 않는다. 쓰기 전 실패로 사건 자체가 저장되지 않았다면 그 사망 수는 유실될 수 있다. 과거 기록·코믹 이력·다른 슬롯 보존 규칙은 기존 transition engine이 담당한다.

복귀 stage는 sequence에서 찾으며 이름을 하드코딩하지 않는다. 정상 clear의 공통 업적은 기존 연결을 유지한다. 저장 후 응답 실패로 업적 전달까지 도달하지 못한 경우의 소급 지급은 이 메뉴안으로 추가 보장하지 않는다.

| 추가 case | 진행 방식 |
| --- | --- |
| 플레이 중 canonical 손상, backup 복구 성공 | 현재 실행 중지. 이전 tick 결과를 backup에 적용하지 않음. 메뉴에서 복구 파일 확인 후 Continue 또는 완료 카드. backup 시점까지 진행·기록이 되돌아갈 수 있음 |
| backup의 stage/HP가 현재와 같음 | 값 비교로 계속하지 않음. 복구가 관측됐다는 사실로 현재 실행 종료 |
| HUD/Flow 읽기에서 먼저 복구, 이후 service 읽기는 Loaded | root별 복구 통지와 Flow의 읽기 직후 검사로 이전 결과 저장 차단 |
| 지속적인 IO 실패 | 플레이 차단 유지. 메뉴도 읽지 못하면 기존 저장 오류 UI로 슬롯 접근 차단. 빈 슬롯으로 위장하거나 자동 생성/삭제하지 않음 |
| schema 2 등 미지원 canonical | 기존 UnsupportedVersion UI. canonical·backup 보존, 자동 변환·backup 덮어쓰기 없음. 초기화는 기존 사용자의 명시적 선택·확인에만 반응 |
| 사용자가 종료 선택 | 이전 HP/death/clear 명령을 재적용하지 않고 기존 종료 port 호출. 다음 정상 실행은 파일을 다시 읽음 |
| 메뉴 버튼 연타·늦은 callback | 해당 popup에서 한 번만 action 실행. disposed/소유권 종료 뒤 callback 무시 |
| 오류 popup이 화면 변경으로 닫힘 | 종료 선택으로 해석하지 않음 |
| 메뉴 route가 즉시 실패 | 현재 실행은 계속 중지. 메뉴/종료 안내 재표시. 저장 재시도 없음 |
| 정상 저장 후 commit 동기 호출/로컬 Iris setup에서 포착된 예외 | 저장 성공 유지. 오류 처리 때문에 재저장하지 않음. 해당 로컬 token/playback 정리 후 실행 중지와 메뉴 안내 |
| BlackReached 이후 UI observer 또는 이미 시작된 비동기 scene 전환 실패 | 저장 결과 유지. 기존 늦은 presentation/coordinator 실패 처리 대상. 이 안이 예외 포착·cover 해제·자동 메뉴 복귀까지 보장하지는 않음 |
| DirectPlay/capture 임시 root의 실패 | 임시 실행 종료. 메뉴에 가면 production 슬롯 표시. 임시 슬롯을 production에 복사하거나 Continue 대상으로 바꾸지 않음 |
| 오류가 없는 상태에서 HP 2로 수동 재시작·메뉴·앱 재실행 | 기존 P1대로 저장 HP 2 유지. 수동 재시작은 일반 기능으로 유지되지만 저장 오류 해결 버튼으로 추가하지 않음 |
| 완료 카드에서 Restart 취소 | 완료 슬롯과 기록 유지 |
| 완료 카드에서 Restart 확인 | 기존 모드 선택·생성 정책에 따라 새 게임 생성. 오류 복구 동작과 구분 |

## 4. 구현 순서와 완료 판정

1. **실행 폐기 보강**: Claimed token 취소, Flow 늦은 callback 방어, BackupRecovered의 service/선행 읽기 차단. 기존 catch-up·pause·강제 clear 차단 유지.
2. **오류 UI 연결 교체**: 전용 Reload 계약/구현/바인딩과 직접 GameClear 복구 callback 삭제. 기존 popup·메뉴 router·quit port 연결. programmatic close와 종료 선택 구분.
3. **기존 메뉴 경로 검증**: 진행 중 Continue, 완료 카드와 명시적 Restart, 읽기 실패/미지원 UI, DirectPlay 격리 확인. 실제 결함이 확인된 범위만 기존 코드 수정.
4. **문구와 문서 정리**: 번역 초안에서 Reload 버튼 삭제, 메뉴/종료 안내로 본문 수정. 실제 localization 적용은 `j2m-localization-atlas` skill의 Draft 승인 및 4개 locale/atlas 절차를 따른다. 승인되지 않은 번역을 이 문서 작성으로 승인된 것으로 간주하지 않는다.
5. **동일 최종 코드 기준 검증**: 아래 tests와 Player 확인을 수행하고 실행 보고서를 갱신. 현재 schema는 변경하지 않으므로 저장 canonical 문서는 필요한 오류 UX 설명만 실제 구현 후 정정.

예상 Scene/Prefab/shared asset 수정은 없다. 기존 Confirm prefab 재사용으로 충분해야 한다. 실제 구현 중 asset 수정이 필요해지면 목적과 Editor/Player 검증을 별도로 기록한다.

## 5. 검증 계획

테스트는 내부 필드 배치보다 관측 가능한 저장 횟수·파일 내용·route·입력 허용 여부를 검사한다.

| 범위/기존 fixture | 필수 판정 |
| --- | --- |
| `CampaignStageFlowTests`, `CampaignSaveServiceTests`, `CampaignSaveArchitectureV2Tests` | HP/death/clear 쓰기 전 실패·저장 후 응답 실패에서 한 번만 commit 시도. 메뉴 이후 이전 결과 재적용 없음. 최종 완료 파일 보존 |
| `CampaignHudReadStoreTests`, `HudChanceInvalidationTests` 및 Flow fixture | 동일 stage/HP 복구, 선행 HUD 읽기 복구, gate 밖 통지, root 격리. 통지 완료 전 observer 재진입·경쟁 mutation 거절, callback 예외 이후 정상 메뉴/새 launch 가능. 복구 후 이전 tick의 추가 mutation 없음 |
| `TerminalSessionAuthorityTests` 및 Flow fixture | 정확한 Claimed token만 해제. 다른 token/coordinator owner 보존. 메뉴의 키보드·패드와 다음 실행의 terminal 입력 가능 |
| 기존 UI integration/production entry fixture | 기본 Submit과 locale 변경 뒤 기본 선택이 메뉴. 명시적 종료, Back/backdrop, programmatic close 뒤 재표시, 중복 callback, 메뉴 route 즉시 거절. Completed는 카드만 표시하며 자동 초기화/엔딩 호출 없음 |
| `CampaignStageFlowTests`의 기존 Iris setup failure fixture | setup 실패 뒤 fallback Launch/LevelFailed publish/clear gate release 없음. 오류 안내 한 번, 저장 한 번, matching playback/token만 정리. Black 이후 실패는 별도 한계로 구분 |
| `CampaignLaunchHandoffPlayModeTests` | 실제 메뉴 Continue가 production router를 통해 저장 모드·HP·stage로 진입. 새 host가 입력 허용 |
| `PlayerMovementPlayModeTests` 및 Flow fixture | 저장 실패 tick 뒤 같은 frame의 catch-up 중단. pause 해제로 재개되지 않음. 강제 clear 차단 |
| `CampaignSlotTransitionCharacterizationTests` 및 save repair fixture | 두 모드 전이·기록 보존 유지. 지속 IO/UnsupportedVersion에 빈 슬롯 표시·자동 삭제 없음 |

해당 worktree에서 repository runner 사용:

```bash
./run_tests.sh core
./run_tests.sh ui
./run_tests.sh full --filter CampaignStageFlowTests
./run_tests.sh full --filter CampaignSaveArchitectureV2Tests
./run_tests.sh full --filter CampaignLaunchHandoffPlayModeTests
```

추가로 실제 수정한 fixture를 선택한다. 0 selected는 통과가 아니다. 기존 결과는 이 수정안 구현 후의 통과 근거로 재사용하지 않는다. unrelated full baseline 실패와 변경 범위 실패를 구분한다. 격리 test root를 사용하고 evidence는 `/mnt/d/J2M/evidence`, Player build는 `/mnt/d/J2M/builds`에 둔다.

Player에서는 피격·사망·클리어 각각의 실패 안내, 메뉴 조작(포인터/키보드/패드), Continue 첫 HUD, 최종 완료 카드, 지속 IO에서 종료, DirectPlay 격리를 확인한다. 정상 캐주얼/하드코어 플레이와 기존 엔딩도 별도 확인한다. 사용자 세이브를 실패 주입 대상으로 쓰지 않는다.

## 6. trade-off와 남는 한계

| 판단 기준 | 이 수정안의 이점 | 비용/한계 |
| --- | --- | --- |
| 구현 난이도 | 전용 Reload route와 완료 화면 복구 분기 제거. 기존 메뉴 launch 사용 | Claimed token 정리와 backup 관측 차단은 별도로 필요. 버튼만 바꾸는 수정은 아님 |
| 일관성 | 평소 Continue와 같은 파일 검증·mode/HP 초기화·완료 카드 사용 | 오류 후 메뉴 이동과 Continue 선택이 필요 |
| 유지보수 | 오류 presenter에서 저장/launch/완료 판단 제거. launch 규칙 중복 감소 | 명시적 종료와 popup 닫힘을 구분하는 작은 adapter 계약 유지 필요 |
| 안정성 | 결과 불명 명령을 다시 실행하지 않아 중복 사망/Chance 차감 방지 | 최신 미저장 결과·기록 유실 가능. backup 복구는 더 이전 상태일 수 있음 |
| 완료 처리 | persisted Completed를 기존 카드로 표시하는 것으로 끝남 | 오류로 건너뛴 엔딩/코믹/업적 전달의 자동 복원은 보장하지 않음 |
| scene 전환 오류 | coordinator 소유권을 침범하지 않음 | 이미 진행 중인 비동기 전환 실패 전체를 해결하는 안은 아님 |

최초 문서 작성·재검토 시에는 runtime 수정과 Unity/Player 검증을 실행하지 않았다. 이후 사용자의 구현 지시에 따라 runtime 수정과 자동 테스트를 실행했다. core 및 선택 fixture는 통과했고 UI에는 번역 미적용 실패 2건이 남았다. 이 계획의 검증 목록 자체는 통과 증거가 아니며 실행 보고서에 실제 결과와 미검증 범위를 구분한다.
