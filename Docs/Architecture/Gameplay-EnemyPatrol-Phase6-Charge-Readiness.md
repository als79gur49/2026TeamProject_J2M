# Enemy Patrol Phase 6 Readiness: `Charge`

이 문서는 phase 5 성공 이후에만 활성 검토 대상이 된다. 현재는 readiness artifact template이며 active rollout truth-source가 아니다.
phase 5 close 승인만으로 이 문서가 자동 활성화되거나 `Charge` rollout이 즉시 오픈되지는 않는다.

## Required Surfaces
- charge start gate
- charge windup / active / recover cadence
- active-phase passive-contact gating
- charge-state write ownership
- patrol-change 영향 한계

## Required Green Suite
- charge start exactness
- charge active cadence
- recover transition
- passive-contact suppression
- replay / hash deterministic

## Gate
- phase 5 문서 / 테스트 완전 종료
- `Forward` fallback untouched
- baseline asset drift `0`
- phase 5 waiver `0`
- phase 5 close 이후 별도 readiness decision artifact 승인
