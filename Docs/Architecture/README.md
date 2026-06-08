# Architecture Docs

이 디렉터리의 canonical architecture entrypoint는 아래 네 문서다.

- [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
  - tick simulation의 canonical architecture spec
- [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)
  - gameplay authoritative boundary를 UI layer까지 확장한 canonical UI architecture spec
- [Gameplay-Rules-Appendix.md](./Gameplay-Rules-Appendix.md)
  - Push/Flip 등 gameplay rule appendix
- [ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md](./ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md)
  - boundary/IR visibility 관련 현재 결정

읽는 순서는 아래를 기준으로 고정한다.

1. [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
2. [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)
3. [Gameplay-Rules-Appendix.md](./Gameplay-Rules-Appendix.md)
4. [ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md](./ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md)

운영 가이드와 baseline은 별도 supporting truth-source다. 이 문서들은 canonical architecture spec을 대체하지 않지만, 현재 runner/governance/evidence 기준을 고정하는 active truth-source로 함께 읽어야 한다.

- [Docs/Testing/Gameplay-Test-Automation-Guide.md](../Testing/Gameplay-Test-Automation-Guide.md)
  - current runner/governance truth for `./run_tests.sh core`, `./run_tests.sh ui`, and PlayMode escalation expectations
- [Docs/Testing/Post-Stage-Content-Bounded-Lane-Operations.md](../Testing/Post-Stage-Content-Bounded-Lane-Operations.md)
  - supporting truth for post-stage-content bounded lane split, Lane A recovery streams, and cross-lane handoff codebook
- [Topology-View-Camera-Canonical-Ownership-2026-04-24.md](./Topology-View-Camera-Canonical-Ownership-2026-04-24.md)
  - slice-local supporting truth for topology/view/camera runtime ownership, helper/glue boundaries, and closure-era sign-off interpretation
- [Topology-Presentation-Fact-Policy.md](./Topology-Presentation-Fact-Policy.md)
  - current supporting truth for canonical topology transition fact normalization at the TickPresentationData build boundary
- [Audio-Architecture-Guidelines.md](./Audio-Architecture-Guidelines.md)
  - current supporting truth for 2D non-spatial audio contracts, runtime ownership, and audio seam vocabulary
- [Gameplay-Audio-Governance.md](./Gameplay-Audio-Governance.md)
  - current supporting truth for gameplay audio semantic-family governance, host one-shot controller scope, and safe semantic expansion protocol
- [Gameplay-Action-Audio-Governance.md](./Gameplay-Action-Audio-Governance.md)
  - current supporting truth for gameplay action-audio profile governance, prefab-local authoring policy, and frozen v1 moment semantics
- [Gameplay-Enemy-Audio-Governance.md](./Gameplay-Enemy-Audio-Governance.md)
  - current supporting truth for prefab-local enemy audio requirement policies/bindings, implicit disabled cue governance, and ChargeActiveLoop policy
- [Gameplay-PushFlip-Fake-Attempt-Policy.md](./Gameplay-PushFlip-Fake-Attempt-Policy.md)
  - current supporting truth for Push/Flip fake attempt classification, movement consume, presentation playback hold, input gate, and known caution points
- [Gameplay-VFX-Governance.md](./Gameplay-VFX-Governance.md)
  - current supporting truth for presentation-only Gameplay VFX lane boundaries, family-specific planners, lifecycle vocabulary, persistent desired state, and existing presenter migration guardrails
- [Enemy-FrontFaceInactive-Visual-Policy.md](./Enemy-FrontFaceInactive-Visual-Policy.md)
  - current supporting truth for campaign enemy inactive-compatible material duplicates, shader contract, bridge shaders, and authoring validation
- [Bgm-Flow-V1-Guidelines.md](./Bgm-Flow-V1-Guidelines.md)
  - current supporting truth for persistent BGM ownership, scene request-source boundaries, request-based BGM playback, FadeOutIn support, and reserved Crossfade governance
- [ADR/ADR-002-Stage-Support-Tree-Deferred-Relocation.md](./ADR/ADR-002-Stage-Support-Tree-Deferred-Relocation.md)
  - active decision record for support tree deferred relocation governance, review triggers, and pilot-eligible gate
- [ADR/ADR-003-Persistent-Bgm-Ownership-Implementation-Gate.md](./ADR/ADR-003-Persistent-Bgm-Ownership-Implementation-Gate.md)
  - active decision record for persistent BGM ownership matrix, unsupported path, and implementation gate
- [ADR/ADR-004-Terrain-Occupancy-Implementation-Gate.md](./ADR/ADR-004-Terrain-Occupancy-Implementation-Gate.md)
  - active decision record for terrain/occupancy vocabulary closure, boundary gate, and slice implementation gate
- [ADR/ADR-006-TileFeature-Overlay-Layer-Gate.md](./ADR/ADR-006-TileFeature-Overlay-Layer-Gate.md)
  - active decision record for SurfaceCell-based TileFeature overlay ownership, occupancy/terrain separation, lazy TileEffect snapshot rules, and presentation-only VFX boundaries
- [Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md](./Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md)
  - supporting truth for `EnemyLogic` patrol owner surface, `RandomWalk` special-case boundary, `Forward` readiness, and `WallFollow` out-of-scope note
- [Gameplay-EnemyPatrol-Decision-Proposal-Contract.md](./Gameplay-EnemyPatrol-Decision-Proposal-Contract.md)
  - supporting truth for patrol proposal contract, supported simple kinds, and owner boundary
- [Gameplay-EnemyPatrol-Phase3-Forward-Commonization.md](./Gameplay-EnemyPatrol-Phase3-Forward-Commonization.md)
  - supporting truth for `Forward` bounded commonization, single proposal seam consumer target, and rollback checklist
- [Gameplay-EnemyPatrol-Forward-Rollout-Gate.md](./Gameplay-EnemyPatrol-Forward-Rollout-Gate.md)
  - supporting truth for quantitative unchanged matrix and post-phase decision gate
- [Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md](./Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md)
  - current supporting truth for `WallFollow` truth table, owner surface, maintain-vs-redesign verdict, and no-touch / rollback gate
- [Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md)
  - current supporting truth for `WindupMelee` final bounded rollout contract, pilot preset scorecard, same-cell ordering, no-touch matrix, and post-close non-claims
- [Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md)
  - current supporting truth for phase 5 official close decision, same-revision targeted evidence bundle, approve/hold branch, and phase 6 boundary
- [Docs/Testing/UI-EditMode-Baseline-2026-04-15.md](../Testing/UI-EditMode-Baseline-2026-04-15.md)
  - pinned UI evidence truth for the completed Stage 1–9 UI architecture baseline
- [Docs/Testing/Full-EditMode-Baseline-2026-04-13.md](../Testing/Full-EditMode-Baseline-2026-04-13.md)
  - broader full-suite baseline context, not the defining truth-source for the Stage 1–9 UI freeze baseline

phase 5 close provenance를 보존하는 아래 문서들은 active supporting truth-source가 아니라 historical supporting note다.

- [Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md)
  - historical supporting note for the runtime parity recovery path used before close approval
- [Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md)
  - historical supporting note for pre-close hard gate hardening and red-state separation
- [Gameplay-EnemyPatrol-Phase5-WindupMelee-Runtime-Fix-Plan.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Runtime-Fix-Plan.md)
  - historical supporting note for the bounded runtime touch set and close-retry-era artifact order

## Enemy Patrol bounded rollout

읽는 순서는 아래를 기준으로 고정한다.

1. [Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md](./Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md)
2. [Gameplay-EnemyPatrol-Decision-Proposal-Contract.md](./Gameplay-EnemyPatrol-Decision-Proposal-Contract.md)
3. [Gameplay-EnemyPatrol-Phase3-Forward-Commonization.md](./Gameplay-EnemyPatrol-Phase3-Forward-Commonization.md)
4. [Gameplay-EnemyPatrol-Forward-Rollout-Gate.md](./Gameplay-EnemyPatrol-Forward-Rollout-Gate.md)
5. [Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md](./Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md)
6. [Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md)
7. [Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md)

이 묶음은 patrol bounded rollout의 active supporting truth-source다.

- phase 2는 `EnemyLogic` 책임 분해, `RandomWalk` special-case 경계, `Forward` readiness, `WallFollow` no-touch 이유를 고정한다.
- proposal contract는 patrol common decision layer가 direction / facing / init hint까지만 제안한다는 owner boundary를 고정한다.
- phase 3는 `Forward` commonization의 single proposal seam consumer 기준과 docs-only defer / rollback checklist를 고정한다.
- rollout gate는 quantitative unchanged matrix와 post-phase decision matrix를 고정한다.
- phase 4는 `WallFollow` truth table, maintain-vs-redesign verdict, bounded redesign gate를 고정한다.
- phase 5 rollout은 `WindupMelee` bounded rollout의 `exact-contract` / `bounded-exposure` drift matrix, pilot preset scorecard, same-cell ordering, sampling matrix, fallback / rollback / success / failure, 그리고 `closed`의 의미를 고정한다.
- phase 5 close execution은 same-revision targeted evidence bundle, close gate, approve / hold branch, current active truth-source vs historical supporting note hierarchy, close wording migration, no-touch list, phase 6 비자동 경계를 고정한다.

## Historical Supporting Notes

아래 문서들은 phase 5 close 당시의 과정과 red-state provenance를 보존하는 historical supporting note다. current active close gate가 아니며, current active truth는 `Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md`다.

- [Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md)
  - historical runtime parity recovery path, patrol-state ownership fixup, and same-revision close-evidence assembly note
- [Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md)
  - historical red-state hardening, baseline-control-first close retry order, and green-vs-red split note
- [Gameplay-EnemyPatrol-Phase5-WindupMelee-Runtime-Fix-Plan.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Runtime-Fix-Plan.md)
  - historical bounded runtime-fix plan, touch-set provenance, and close-retry-era artifact order

## Inactive Readiness Templates

아래 문서들은 phase 5 close 이후에도 자동 활성화되지 않는 readiness artifact template이다.

- [Gameplay-EnemyPatrol-Phase6-JumpChaser-Readiness.md](./Gameplay-EnemyPatrol-Phase6-JumpChaser-Readiness.md)
  - inactive readiness template for a separate `JumpChaser` review after phase 5 close
- [Gameplay-EnemyPatrol-Phase6-Charge-Readiness.md](./Gameplay-EnemyPatrol-Phase6-Charge-Readiness.md)
  - inactive readiness template for a separate `Charge` review after phase 5 close

historical/non-canonical 문서는 더 이상 이 디렉터리의 active truth-source가 아니다.

- archive index: [Docs/Archive/README.md](../Archive/README.md)
- archived architecture docs: [Docs/Archive/Architecture](../Archive/Architecture)
