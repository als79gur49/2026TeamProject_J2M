# ADR-002 Stage Support Tree Deferred Relocation

- Status: Accepted
- Date: 2026-04-22

## Context

stage-content runtime canonical path는 이미 `StageContentEntry -> StagePresentationDefinition` reference 체인으로 닫혀 있다. 다만 stage-specific enemy/static presentation catalog, prefab, profile, support asset의 물리적 위치를 canonical content package 안으로 실제 relocation할지는 아직 별도 판단이 필요하다.

## Definitions

- `consumed asset complete`
  - runtime이 실제로 소비하는 stage presentation 자산이 모두 `StageContentEntry -> StagePresentationDefinition` reference 체인 안에서 닫혀 있는 상태다.
  - asset의 물리적 위치가 content package 안인지 밖인지는 이 정의에 포함되지 않는다.
- `support tree full relocation`
  - enemy/static presentation catalog, prefab, profile, support asset의 물리적 저장 위치와 authoring 구조를 canonical content package 쪽으로 실제 이동시키는 작업이다.
  - 단순 reference canonicalization과 다르다.

## Decision

- 현재 decision은 `deferred`다.
- 이 `deferred`는 “미정”이 아니라 “현재는 relocation 미실행, 재검토 조건 고정” 상태를 뜻한다.
- runtime-consumed canonical entrypoint는 계속 `StagePresentationDefinition`만 인정한다.
- support tree가 canonical content package 밖에 있다는 이유만으로 compat residue로 분류하지 않는다.

## Scheduled Trigger

- 다음 stage-specific support tree가 추가되는 시점
- 또는 다음 architecture checkpoint 문서 갱신 시점

## Immediate Trigger

- 현재 support tree 위치 때문에 reference audit blind spot이 생김
- stage-specific support asset duplication/copy churn이 발생함
- new stage authoring에서 support tree discoverability 혼선 이슈가 반복됨
- relocation pilot 요청이 stage/presentation owner에게 공식 backlog로 접수됨

## Review Owner And Inputs

- primary owner:
  - stage presentation / authoring owner
- co-owner:
  - architecture reviewer
- reviewer set:
  - validator/audit owner
  - asset migration reviewer
- required review inputs:
  - 최신 support asset inventory
  - `StagePresentationDefinition` reference map
  - GUID/reference risk memo
  - authoring friction 사례
  - candidate relocation map
  - revert plan

## Interim Policy While Relocation Is Deferred

- runtime-consumed reference의 canonical entrypoint는 계속 `StagePresentationDefinition`만 인정한다.
- scene serialized fallback reference는 금지한다.
- 새 support asset를 추가할 때는 stage-specific tree naming rule과 inventory 기록을 유지한다.
- support tree가 바깥에 있다는 이유만으로 stage-content canonical path를 reopen하지 않는다.

## Pilot-Eligible Gate

- decision record가 `deferred -> pilot-eligible`로 갱신됨
- canonical path 무변경 확인
- one-stage-only pilot 범위 확정
- GUID/reference dry-run audit 완료
- revert plan과 validator update plan이 같은 change set에 준비됨

## Non-Goals

- immediate relocation 강행
- stage-content runtime canonical path 재설계
- support tree 위치를 runtime bug로 오인하는 것
