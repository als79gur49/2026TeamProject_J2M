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
- [Audio-Current-Structure-Source.md](./Audio-Current-Structure-Source.md)
  - external current-structure source for audio documentation regeneration, stale-token audits, and current Push/Flip action-audio moment policy
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
- [Enemy-AI-Naming-Guidelines.md](./Enemy-AI-Naming-Guidelines.md)
  - current supporting truth for ownership-based Enemy AI profile/core/brain/capability/view/animator/presentation naming
- [Enemy-AI-Current-Structure-Source.md](./Enemy-AI-Current-Structure-Source.md)
  - current supporting truth for Phase 1 Enemy AI profile root, runtime definition lanes, Standard-only Charge BehaviorModule production content, and Phase 2 trigger boundaries
- [Enemy-AI-Phase1-Merge-Gate.md](./Enemy-AI-Phase1-Merge-Gate.md)
  - current supporting truth for Phase 1 merge checklist, validation evidence wording, reviewer focus, and forbidden follow-up pattern scans
- [Enemy-AI-Shield-Summon-Utility-Audit.md](./Enemy-AI-Shield-Summon-Utility-Audit.md)
  - current supporting truth for Shield pre-design, Summon/Utility audit boundaries, and Phase 2 trigger classification
- [Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md](./Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md)
  - current supporting truth for Option C spawn/entity creation seam extraction for Utility Summon, where Summon remains in the Utility capability lane and Option B/SummonBehaviorModule remains future-only
- [Bgm-Flow-V1-Guidelines.md](./Bgm-Flow-V1-Guidelines.md)
  - current supporting truth for persistent BGM ownership, scene request-source boundaries, request-based BGM playback, FadeOutIn support, and reserved Crossfade governance
- [ADR/ADR-002-Stage-Support-Tree-Deferred-Relocation.md](./ADR/ADR-002-Stage-Support-Tree-Deferred-Relocation.md)
  - active decision record for support tree deferred relocation governance, review triggers, and pilot-eligible gate
- [ADR/ADR-003-Persistent-Bgm-Ownership-Implementation-Gate.md](./ADR/ADR-003-Persistent-Bgm-Ownership-Implementation-Gate.md)
  - active decision record for persistent BGM ownership matrix, unsupported path, and implementation gate
- [ADR/ADR-004-Terrain-Occupancy-Implementation-Gate.md](./ADR/ADR-004-Terrain-Occupancy-Implementation-Gate.md)
  - historical decision record superseded by ADR-007; occupancy lane ownership remains active
- [ADR/ADR-007-Runtime-Terrain-Truth-Removal.md](./ADR/ADR-007-Runtime-Terrain-Truth-Removal.md)
  - active decision record for runtime terrain truth removal, terrain-free in-bounds cells, and remaining blocker vocabulary
- [ADR/ADR-006-TileFeature-Overlay-Layer-Gate.md](./ADR/ADR-006-TileFeature-Overlay-Layer-Gate.md)
  - active decision record for SurfaceCell-based TileFeature overlay ownership, terrain-free boundary, lazy TileEffect snapshot rules, and presentation-only VFX boundaries
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
  - historical supporting truth for the retired `WindupMelee` repository profile rollout, pilot preset scorecard, same-cell ordering, no-touch matrix, and post-close non-claims
- [Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md](./Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md)
  - historical supporting truth for phase 5 official close decision, same-revision targeted evidence bundle, approve/hold branch, and phase 6 boundary
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

이 묶음은 patrol bounded rollout의 active/historical supporting truth-source다. Phase 5 `WindupMelee` 문서는 retired repository profile의 historical provenance이며, current production windup lane은 explicit `WindupProjectile` profile path다.

- phase 2는 `EnemyLogic` 책임 분해, `RandomWalk` special-case 경계, `Forward` readiness, `WallFollow` no-touch 이유를 고정한다.
- proposal contract는 patrol common decision layer가 direction / facing / init hint까지만 제안한다는 owner boundary를 고정한다.
- phase 3는 `Forward` commonization의 single proposal seam consumer 기준과 docs-only defer / rollback checklist를 고정한다.
- rollout gate는 quantitative unchanged matrix와 post-phase decision matrix를 고정한다.
- phase 4는 `WallFollow` truth table, maintain-vs-redesign verdict, bounded redesign gate를 고정한다.
- phase 5 rollout은 retired `WindupMelee` repository profile cleanup 이전 bounded rollout의 `exact-contract` / `bounded-exposure` drift matrix, pilot preset scorecard, same-cell ordering, sampling matrix, fallback / rollback / success / failure, 그리고 `closed`의 의미를 보존한다.
- phase 5 close execution은 same-revision targeted evidence bundle, close gate, approve / hold branch, close wording migration, no-touch list, phase 6 비자동 경계를 historical provenance로 보존한다.

## Stage clear save/profile boundary

- Stage clear profile은 save/profile boundary의 current vocabulary다.
- 저장 모델은 `StageClearProfileSnapshot`, `PlayerStageClearRecord`, `IStageClearProfileStore`, `SaveSlotStageClearProfileStore`를 사용한다.
- 저장 DTO schema는 `StageClearProfileSnapshot`, `ClearRecordsByStageId`, `HasAttempted`, `ProcessedClearAttemptIds` vocabulary만 쓴다.
- 현재 테스트 단계에서는 old save compatibility와 migration adapter를 제공하지 않는다.
- Production save slot PlayerPrefs read/write key는 `Game.Feature.Stages.StageClearSaveSlots` / `Game.Feature.Stages.ActiveStageClearSaveSlot`이다.
- Old PlayerPrefs key `Game.Feature.Stages.SaveSlots` / `Game.Feature.Stages.ActiveSaveSlot`은 delete-only cleanup 대상이며 production read/write path에 사용하지 않는다.
- Direct-play temp key `Game.Feature.Stages.DirectPlay.TempSaveSlots` / `Game.Feature.Stages.DirectPlay.TempActiveSaveSlot`은 production key split 대상이 아닌 별도 임시 namespace다.
- Stage clear save root DTO는 `SchemaId = StageClearSaveSlots`, `SchemaVersion = 2` marker를 쓴다. `StageClearProfileSnapshot.Version`은 profile snapshot version이며 root schema marker와 다른 개념이다.
- Current key contamination 또는 invalid payload는 `SaveSlotStore.LoadDto()` raw JSON read 직후 검사한다. old save compatibility는 제공하지 않고, legacy/corrupt payload는 rejected/reset되며 store/API에 노출되지 않는다.
- Legacy/corrupt payload reset은 non-crash path이고 empty current database로 닫힌다. `StageClearSaveLoadReport`는 logic-level report로만 남기며 Diagnostics overlay 연결은 이번 PR 범위가 아니다.
- UI notification, HUD banner, popup, screen 표시도 이번 PR 범위가 아니다.
- Progression unlock graph와 player clear record는 다른 개념이다. Stage objective clear와 `MinimalStageCompletionReadModel` 기반 StageResult continue/retry flow는 runtime/UI canonical path로 유지한다.
- Retired reward/evaluation/progression residue fields must not re-enter save/profile production DTOs.

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
