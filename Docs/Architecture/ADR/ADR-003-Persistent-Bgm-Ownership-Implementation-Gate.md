# ADR-003 Persistent BGM Ownership Implementation Gate

- Status: Accepted
- Date: 2026-04-22
- Last updated: 2026-09-12

## Context

persistent BGM ownership은 stage-content canonical path와 분리된 별도 lane으로 유지한다. `StageRuntimeBuilder` / `StageRuntimeBuildResult` gameplay-only boundary를 넓히지 않은 채, requester, bootstrap, persistent runtime owner, playback capability owner를 분리해서 운영해야 한다.

## Phase Split

- `D0. discovery`
  - 현재 owner surface, bootstrap path, registry, requester, playback capability를 조사한다.
- `D1. decision closed`
  - ownership matrix, unsupported path, continuity policy, transition policy를 ADR/decision record로 잠근다.
  - 이 단계는 implementation start가 아니다.
- `D2. implementation gate ready`
  - gate가 green일 때만 implementation lane을 연다.
- `D3. implementation`
  - gate 충족 후 bounded implementation slice만 허용한다.

## Ownership Matrix

- requester:
  - `SceneBgmRequestSource`
- scene-local installer access seam:
  - same-root `AudioRuntimeInstaller`
- persistent runtime owner:
  - `GlobalAudioFlowRoot`
- playback capability owner:
  - `IBgmPlaybackPort` and shared audio runtime capability

## Unsupported Path

- `GameplayAudioPresentationController` does not own BGM.
- no scene-global lookup
- no builder/result ownership
- single-source `FadeOutIn`만 current v1 capability다. true `Crossfade` completion claim은 허용하지 않는다.
- no generic cross-domain dispatcher
- stage data는 neutral reference만 유지한다.

## Same-Root Wiring Contract

- canonical root same `GameObject`에 `AudioRuntimeInstaller`와 `GlobalAudioFlowBootstrap`이 co-located 되어야 한다.
- installer mode는 `PreferRegisteredPersistentRuntime`로 고정한다.
- scene-local installer는 access seam일 뿐, persistent owner를 대체하지 않는다.

## D2 Gate

- ownership matrix 승인
- unsupported path 명시 완료
- same-root wiring contract 확정
- minimal test harness 준비
  - coordinator unit tests
  - registry/bootstrap targeted tests
  - `UIAudioScene` continuity smoke harness
  - reporting wording doc test

## Implementation Open Rule

- `D1` decision closed
- `D2` gate green
- implementation scope는 아래 중 하나로 bounded
  - `Immediate continuity hardening`
  - `bootstrap hardening`
  - `capability additive seam`

## Non-Goals

- decision closed를 implementation approved로 해석하는 것
- true `Crossfade` roadmap을 v1 current capability로 오인하는 것
- stage-content path에 ownership logic를 밀어넣는 것

## Follow-up Decision: Request Claim Lifecycle

2026-09-12 lifecycle re-audit에서 scene-scoped requester가 제출한 요청이 persistent `BgmRequestRouter`에 남는 ownership gap을 확인했다. 이 절은 기존 persistent-root, same-root access seam, playback capability ownership 결정을 변경하지 않고 request lifecycle 계약을 보완한다.

이 결정은 `4c4d88c45`에서 구현·검증되었으며, same-revision 근거와 완료 마커 `BGM_LC_001_IMPLEMENTED_AND_VALIDATED`는 lifecycle re-audit 문서에 기록되어 있다. 따라서 아래 항목은 미구현 제안이 아니라 현재 유지해야 할 계약과 회귀 검증 gate다.

상세 조사 근거와 구현 후보 비교는 [`Bgm-System-Lifecycle-Reaudit-2026-09-12.md`](../../Refactor/Bgm-System-Lifecycle-Reaudit-2026-09-12.md)를 따른다.

### Claim Semantics

- `BgmFlowRequest`는 fire-and-forget 신호가 아니라 router가 보관하는 persistent claim이다.
- claim validity는 priority arbitration보다 먼저 lifecycle ownership으로 결정한다.
- priority는 현재 유효한 claim 중 실행할 요청만 선택하며, 파괴된 requester의 claim을 유효하게 만들 수 없다.
- Profile claim과 explicit Stop claim은 동일한 lifecycle 규칙을 따른다.

### Owner-Token Lease

- request 등록은 owner-scoped, idempotent-disposal lease를 반환해야 한다.
- router는 source kind별 current claim과 generation token을 함께 보관한다.
- release token이 current token과 일치할 때만 해당 claim을 제거한다.
- 이전 owner의 늦은 release처럼 token이 일치하지 않는 cleanup은 no-op이다.
- 같은 source kind의 새 claim은 기존 claim을 대체하는 latest-wins 정책을 유지한다.
- 대체된 이전 claim은 새 claim 철회 뒤에도 부활하지 않는다.
- 유효 claim 철회 후 router는 남아 있는 최고 우선순위 claim을 재평가한다.

### Lifecycle Owners

- `SceneBgmRequestSource`는 자신이 제출한 `SceneDefault` claim의 lease를 scene lifetime 동안 소유한다.
- `StageAudioRuntimeRequestSource`는 resolved stage audio를 Profile 또는 Stop claim으로 변환하는 stateless translator이며 Stage lifetime을 소유하지 않는다.
- `StageBackedGameplaySceneInstallerBase` 또는 동등한 Stage session lifecycle owner가 `StageGameplay` claim의 lease를 소유하고 teardown에서 해제한다.
- persistent router는 Unity scene 또는 requester 생존 여부를 검색하지 않고 token validation과 arbitration만 소유한다.

### Empty Selection And Silence

- 유효 claim이 0개가 되면 router selection은 비우되 현재 playback을 즉시 중단하지 않는다.
- 이 동작은 `LoadSceneMode.Single` 전환에서 old-owner teardown과 destination claim 등록 사이의 일시적인 공백 때문에 동일 BGM이 불필요하게 stop/restart 되는 것을 막기 위한 continuity policy다.
- explicit Stop claim만 authoritative silence를 뜻한다.
- 이 정책은 모든 canonical destination이 `SceneDefault` 또는 `StageGameplay` Profile/Stop claim을 제출한다는 scene contract를 전제로 한다.
- strict stop-on-empty가 필요해지면 stale claim을 허용하지 말고 별도의 atomic scene-transition handoff contract를 결정해야 한다.

### Rejected Minimal Alternatives

- priority 변경은 stale claim을 제거하지 않고 실패 방향만 `Stage -> MainMenu`에서 `MainMenu -> Stage`로 바꾸므로 사용하지 않는다.
- `Clear(BgmRequestSourceKind)`는 이전 owner의 늦은 teardown이 같은 kind의 더 새로운 claim을 제거할 수 있으므로 사용하지 않는다.
- scene transition entrypoint에서의 global clear는 direct load, 실패 복구, additive load와 audio policy를 결합하므로 primary lifecycle contract로 사용하지 않는다.

### Validation Gate

implementation completion claim에는 최소한 아래 same-revision evidence가 필요하다.

- Stage Profile claim 철회 후 MainMenu `SceneDefault` 선택
- Stage Stop claim 철회 후 MainMenu BGM 복원
- 같은 kind의 새 claim 등록 후 이전 token의 늦은 release가 새 claim을 보존함
- 대체된 이전 claim이 새 claim 철회 뒤 부활하지 않음
- suppression 중 claim 철회가 playback을 실행하지 않고 suppression 종료 시 최신 유효 selection만 복원함
- 실제 `UIAudioScene -> MainMenuScene` `LoadSceneMode.Single` 전환
- 동일 profile의 cross-scene continuity가 불필요한 restart를 만들지 않음
