# Enemy Patrol Phase 6 Readiness: `JumpChaser`

이 문서는 phase 5 성공 이후에만 활성 검토 대상이 된다. 현재는 readiness artifact template이며 active rollout truth-source가 아니다.
phase 5 close 승인만으로 이 문서가 자동 활성화되거나 `JumpChaser` rollout이 즉시 오픈되지는 않는다.

## Required Surfaces
- jump windup / airborne / cooldown owner surface
- patrol-vs-jump suppression matrix
- landing retry semantics
- patrol-state footprint boundary

## Required Green Suite
- jump start drift
- jump landing parity
- jump cooldown resume
- passive-contact coexistence
- replay / hash deterministic

## Gate
- phase 5 문서 / 테스트 완전 종료
- `Forward` fallback untouched
- baseline asset drift `0`
- phase 5 waiver `0`
- phase 5 close 이후 별도 readiness decision artifact 승인
