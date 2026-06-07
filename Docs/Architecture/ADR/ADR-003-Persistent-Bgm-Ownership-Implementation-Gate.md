# ADR-003 Persistent BGM Ownership Implementation Gate

- Status: Accepted
- Date: 2026-04-22

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
- no fade/crossfade completion claim in v1
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
- fade/crossfade roadmap을 v1 current capability로 오인하는 것
- stage-content path에 ownership logic를 밀어넣는 것
