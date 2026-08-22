# Architecture Docs

이 디렉터리의 canonical architecture entrypoint는 아래 네 문서다.

- [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
  - tick simulation의 canonical architecture spec
- [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)
  - gameplay authoritative boundary를 UI layer까지 확장한 canonical UI architecture spec
- [Climate-Crisis-KR-Typography-Migration-Closeout.md](./Climate-Crisis-KR-Typography-Migration-Closeout.md)
  - current Climate Crisis KR asset, 19-role, authored-sizing, layout, glyph, visual-evidence closeout
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

- [Platform-Runtime-Foundation.md](./Platform-Runtime-Foundation.md)
  - current store-neutral provider request, resolution, Local default, and application lifecycle contract
- [Product-Achievement-Foundation.md](./Product-Achievement-Foundation.md)
  - product-global achievement IDs, earned ledger, pending publication outbox, atomic persistence, and store-neutral publication boundary
- [Docs/Testing/Platform-Provider-Selection-Validation.md](../Testing/Platform-Provider-Selection-Validation.md)
  - focused source-only selection matrix, production-boundary probes, and mutation evidence rules
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
  - current supporting truth for Option C spawn/entity creation seam extraction for Utility Summon and the later test-local Behavior Summon runtime/emitter path, where production Summon remains in the Utility capability lane and production migration remains future-only
- [Enemy-AI-Summon-Duplicate-Guard-Design.md](./Enemy-AI-Summon-Duplicate-Guard-Design.md)
  - current supporting truth for the implemented compile-skeleton guard that Behavior Summon must fail-fast when authored alongside existing Utility SummonMinion on the same Enemy AI profile
- [Enemy-AI-Summon-Behavior-Runtime-State-Design.md](./Enemy-AI-Summon-Behavior-Runtime-State-Design.md)
  - current supporting truth for the implemented test-local Option B mutable Summon behavior runtime state/emitter shape, ownership boundaries, parity matrix, migration outline, and remaining production migration boundary
- [Enemy-AI-Summon-Asset-Migration-Plan.md](./Enemy-AI-Summon-Asset-Migration-Plan.md)
  - current supporting truth for the future Option B Utility SummonMinion to Behavior Summon asset field mapping, migration order, residue policy, rollback strategy, and test matrix without migrating production assets
- [Enemy-AI-Summon-Asset-Scoped-Migration-Readiness.md](./Enemy-AI-Summon-Asset-Scoped-Migration-Readiness.md)
  - current readiness / dry-run gate for exact production Summoner allowlist, current Utility field inventory, duplicate guard sequencing, replay/hash no-double-count policy, baseline capture, residue scans, rollback, and validation commands before any production asset migration
- [Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md](./Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md)
  - current supporting truth for Option B replay trace, event log, determinism hash, export naming, and source metadata compatibility while preserving replay/export-visible names
- [Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md](./Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md)
  - current supporting truth for Option B Summon presentation signals, audio cues, VFX cues, view binding, visibility changes, implemented test-local full presentation/audio/VFX parity, preserved current cue semantics, and compatibility policy without changing assets
- [Enemy-AI-Summon-Option-B-Implementation-Plan.md](./Enemy-AI-Summon-Option-B-Implementation-Plan.md)
  - current supporting truth for Option B implementation slices, implemented compile-skeleton, test-local runtime/emitter status, test-local presentation/audio/VFX parity status, guard/test/migration order, validation gates, rollback strategy, and non-goal boundaries
- [Enemy-AI-Summon-Option-B-First-Code-Slice-Closeout.md](./Enemy-AI-Summon-Option-B-First-Code-Slice-Closeout.md)
  - current closeout report for the Summon Option B first code slice, recording compile-skeleton-only status, preserved contracts, scans, validation evidence, and handoff to the implemented test-local runtime/emitter parity slice
- [Enemy-AI-Summon-Behavior-Runtime-Emitter-Parity-Closeout.md](./Enemy-AI-Summon-Behavior-Runtime-Emitter-Parity-Closeout.md)
  - current closeout report for the Summon Behavior runtime/emitter and test-local presentation/audio/VFX parity slices, recording implementation status, preserved Spawn/EntityCreation seam, production asset non-migration, replay/export name preservation, presentation/audio/VFX name preservation, scans, and validation evidence
- [Enemy-AI-Summon-Asset-Scoped-Migration-Closeout.md](./Enemy-AI-Summon-Asset-Scoped-Migration-Closeout.md)
  - current closeout report for the production ArchetypeSummoner asset-scoped Utility SummonMinion to Behavior Summon migration, including migrated asset allowlist, field mapping, capability handling decision, guard/residue results, replay/presentation validation, full-lane status, rollback path, and non-goals
- [Enemy-AI-Summon-Legacy-Capability-Cleanup-Closeout.md](./Enemy-AI-Summon-Legacy-Capability-Cleanup-Closeout.md)
  - current closeout report for removing the empty ArchetypeSummoner legacy Utility capability asset/reference while retaining Summon Behavior ownership, common PassiveContact, SourceEffectIndex / Effect compatibility, generic Utility compatibility code, and Charge no-op boundaries
- [Enemy-AI-Utility-Summon-Code-Retirement-Closeout.md](./Enemy-AI-Utility-Summon-Code-Retirement-Closeout.md)
  - current closeout report for retiring executable Utility Summon authoring, compile, runtime progression, trigger emission, and spawn-request conversion while retaining enum tombstones, Behavior Summon ownership, GravityFieldAura Utility execution, and presentation/replay compatibility names
- [Enemy-AI-Summon-Internal-Type-Decoupling-Closeout.md](./Enemy-AI-Summon-Internal-Type-Decoupling-Closeout.md)
  - Slice B closeout report for internal Summon DTO and presentation carrier decoupling; its legacy raw `utilityKind: 3` adapter notes are superseded by C1b and the final ownership closeout
- [Enemy-AI-Summon-Vfx-Vocabulary-Migration-C1a-Closeout.md](./Enemy-AI-Summon-Vfx-Vocabulary-Migration-C1a-Closeout.md)
  - current closeout report for Slice C1a Summon VFX code vocabulary rename, preserving cue numeric values, serialized binding compatibility, audio, and replay/export deferrals
- [Enemy-AI-Summon-Presentation-Prefab-Migration-C1b-Readiness.md](./Enemy-AI-Summon-Presentation-Prefab-Migration-C1b-Readiness.md)
  - readiness report for Slice C1b production Summon presentation prefab migration, including driver ownership, raw `utilityKind: 3` history, GUID-preserving serialization strategy, operation order, and C2 boundaries
- [Enemy-AI-Summon-Presentation-Prefab-Migration-C1b-Closeout.md](./Enemy-AI-Summon-Presentation-Prefab-Migration-C1b-Closeout.md)
  - closeout report for Slice C1b production Summon scale pulse component rename, JPeter prefab typed binding migration, legacy raw-3 adapter removal, Gravity raw `2` preservation, validation plan, and rollback
- [Enemy-AI-Summon-Replay-Export-Vocabulary-C2-Readiness.md](./Enemy-AI-Summon-Replay-Export-Vocabulary-C2-Readiness.md)
  - read-only C2 decision record for retaining Effect / SourceEffectIndex replay/export vocabulary as shared compatibility contracts, with external migration closed as a no-op
- [Enemy-AI-Summon-Behavior-Ownership-Final-Closeout.md](./Enemy-AI-Summon-Behavior-Ownership-Final-Closeout.md)
  - final umbrella closeout for Summon Behavior ownership, Utility Summon executable retirement, presentation/prefab/VFX migration status, retained replay/source-correlation compatibility vocabulary, local acceptance evidence, and project-wide red separation

For current Summon production ownership, read the final umbrella closeout first. Earlier Summon readiness, plan, and slice closeout documents preserve sequence and historical decisions; the final umbrella supersedes intermediate "current" wording where later slices changed the state.
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
- 저장 모델은 `StageClearProfileSnapshot`, `PlayerStageClearRecord`를 사용하며 profile slot mutation은 `ICampaignSaveSlotStore.UpdateSlot` 경계로 수행한다. `IStageClearProfileStore`는 clear-flow domain seam이며 별도 campaign persistence 구현을 두지 않는다.
- Production campaign progression save truth는 `Saves/profile.json` 하나다. PlayerPrefs progression import는 지원하지 않는다. Atomic writer의 `<file>.rollback`은 replace fallback 중단 복구용 구현 artifact이며 legacy progression rollback이나 별도 save schema가 아니다.
- Physical — current `Saves/profile.json`은 `CampaignProfileDocument`의 `SchemaVersion = 2`이다. 각 `Slots[]`의 `StageClearProfileSnapshot`은 `CampaignStageClearProfileDocument`이고 `Records[]`를 사용하며, record에는 `StageId`, `HasAttempted`, `HasCleared`, `ClearCount`, record-level `ProcessedStageRunIds : string[]`가 저장된다. profile snapshot에는 이와 별개인 profile-level `ProcessedStageRunIds : string[]`와 `ProcessedClearAttemptIds : string[]`가 저장된다. 이전 profile `SchemaVersion = 1`은 읽거나 승격하지 않는다.
- `StageClearProfileSnapshot.Version`은 profile snapshot version이며 root `CampaignProfileDocument.SchemaVersion`과 다른 개념이다.

| Processed-ID surface | `profile.json` | Current Production semantic use | Policy |
| --- | --- | --- | --- |
| Profile `ProcessedStageRunIds` | yes | 없음 | first-public schema freeze |
| `ProcessedClearAttemptIds` | yes | 없음 | first-public schema freeze |
| Record `ProcessedStageRunIds` | yes | 없음 | first-public schema freeze |

- Semantic — current Production clear path는 `GameplayHostPresentationFeed -> CampaignGameplayFlowController -> ICampaignSaveSlotStore.UpdateSlot -> CampaignLaunchStateRepairingCampaignSaveSlotStore -> CampaignSaveSlotStoreAdapter -> CampaignSaveService.UpdateSlot -> FileCampaignProfileRepository.Save -> Saves/profile.json`이다. 이 path는 stage cursor, level group, completion state, chances, timestamp를 갱신하지만 processed run/attempt ID를 생성하거나 active idempotency mechanism으로 검사하지 않는다.
- Preservation — `CampaignSaveSlotStoreAdapter`, mapper, repository는 existing processed-ID collections를 load/map/round-trip/save하며 보존한다. 이는 first-public schema preservation이지 current gameplay-generated idempotency state의 active consumption이 아니다.
- Compatibility — 세 processed-ID field는 first-public persisted surface이므로 ordinary dead-code cleanup으로 제거할 수 없다. removal에는 별도 schema/release exposure review가 필요하며, 이는 새 active business requirement 또는 semantic endorsement가 아니다.
- Persisted processed-ID 이름은 plain string field이며 active typed runtime identity에서 persisted ID로 이어지는 current flow를 뜻하지 않는다.
- `CampaignSaveService.ApplyStageClear`는 reference/legacy idempotent command semantics를 유지하지만 current Production caller가 없고 current Production gameplay clear entry path가 아니다. current Production composition에서는 dormant이며 별도 ownership/compatibility cleanup review 전까지 dead API로 단정하지 않는다.
- Production active launch pointer는 non-Cloud `Saves/local-launch-state.json` 하나다. 파일 missing은 committed active 없음으로 처리하며 PlayerPrefs fallback을 사용하지 않는다. Profile canonical missing은 valid current-schema backup을 먼저 복구한 뒤에만 no-save로 분류하고, unknown schema canonical은 older backup으로 덮지 않는다.
- Local active는 gameplay installer가 non-empty profile slot과 request/context/resolved/profile stage identity를 검증한 뒤 commit한 slot이다. MainMenu NewGame/Restart/Continue는 active를 쓰지 않는다.
- Pending launch는 slot/stage/navigation/source/token을 묶는 application-session `CampaignLaunchHandoffSessionStore`가 소유하며 first accepted request wins와 matching-token clear/consume을 적용한다. MainMenu NewGame/Restart/empty Continue는 profile mutation 전에 complete handoff를 reservation으로 선점하고, confirmation callback은 captured token/slot/operation kind가 current일 때 한 번만 실행된다. Intro comic terminal callback도 token/slot/stage/navigation/source exact match를 재검증하고 operation당 한 번만 처리한다. stale callback은 profile/progress/routing/context/active/pending에 영향을 주지 않는 no-op이며 reservation 성공은 persistent active commit이 아니다.
- Pending은 intro comic sequence와 scene transition을 통과하지만 failure/cancel/rejection/load/installer validation failure에서 exact request만 clear되고, process restart에서는 복원되지 않는다. `StageLaunchContext`는 token/slot/stage/navigation/source full identity를 가진 in-memory owner이며 동일 exact identity의 중복 등록도 거절하는 strict first-owner-wins와 exact clear/consume을 적용한다. Async callback은 captured context ownership을 재검증하고 late callback은 newer operation을 변경하지 않는다. Installer는 active-write attempt 이후 예외에도 previous active를 복원하며 active/running/pending/context를 하나의 commit transaction으로 처리하고 성공 시 pending/context를 finalize한다. Pending 없는 active fallback은 명시된 Retry/NextStage reload source에만 허용된다. CurrentScene/Configured route guard rejection은 immediate failure로 surface되고 기존 owner를 보존한다. Coordinator의 asynchronous DirectPlay 복구도 transition 시작 시 captured ownership generation이 그대로일 때만 수행하며 newer set/clear 뒤에는 no-op이다. Subsystem registration은 runtime context만 reset하며 Editor DirectPlay SessionState prime은 consume 또는 explicit editor cleanup까지 유지된다. Comic sequence Completed의 `IntroComicCompleted`는 gameplay route의 immediate acceptance 이후에만 기록되고, rejection/exception에는 기록되지 않는다. Comic sequence callback은 persistent active를 쓰지 않는다.
- `CampaignRunningSlotContext`는 active commit 직후 생성되는 scene-local mutation identity이며 이후 active 변경과 무관하게 고정된다.
- Old PlayerPrefs campaign progression/active key는 production read/write/delete path에 사용하지 않는다. 기존 값은 무시하며 broad cleanup은 별도 승인 없이는 금지한다.
- Schema-3 `SaveSlotStore` DTO, campaign PlayerPrefs backend, active-slot PlayerPrefs backend, key 기반 DirectPlay context는 제거되었다. 런타임 campaign 저장 구현은 schema-2 profile과 schema-1 local state만 가진다.
- Campaign temp DirectPlay는 같은 canonical JSON 계약을 격리된 root에서 사용한다. Editor root는 `<project>/Library/J2M/DirectPlayCampaign/Saves`, Player diagnostics root는 process-lifetime GUID를 포함한 `Application.temporaryCachePath/J2M/PlayerCaptureCampaign/<process-guid>/Saves`다.
- Production Campaign DirectPlay의 explicit active overwrite/prime은 editor launch exception으로 유지되며 normal production handoff를 생성하거나 소비하지 않는다.
- DirectPlay context는 저장 키를 운반하지 않고 `CampaignTempSlot` mode만으로 temporary composition을 선택한다. 정상 Player 종료 cleanup은 containment와 GUID leaf를 검증한 자기 process scope만 제거하며 다른 run directory는 건드리지 않는다.
- Campaign file store는 save root당 단일 in-process writer를 전제로 한다. Cross-process writer coordination과 transaction commit-marker schema는 current contract가 아니다.
- Current-schema profile load는 음수 slot counter와 persisted nested performance/stage-clear record를 normalization 전에 검증한다. 음수 counter, invalid/non-canonical/duplicate record와 invalid processed ID는 `InvalidDocument`로 fail-closed하며 unrelated mutation에서 조용히 clamp·축약 저장하지 않는다. `RemainingChances == 0`의 current sentinel 의미는 유지한다.
- `LoadAllWithReport`만 blocked profile을 UI 진단용 empty placeholder로 투영한다. report를 반환하지 않는 `LoadAll`/`LoadSlot`과 모든 campaign mutation 경계는 blocked status에서 예외로 중단하고 repository write를 수행하지 않는다.
- Destructive profile commit은 canonical과 backup의 interrupted-write rollback을 snapshot 전에 정규화한다. Blocked reset은 backup을 canonical보다 먼저 정규화·격리하고 empty reset profile의 durable write 뒤에만 pending marker를 제거한다.
- UI notification, HUD banner, popup, screen 표시도 이번 PR 범위가 아니다.
- Progression unlock graph와 player clear record는 다른 개념이다. Stage objective clear와 `MinimalStageCompletionReadModel` 기반 StageResult continue/retry flow는 runtime/UI canonical path로 유지한다.
- Transient terminal completion boundary는 `StageClearResult = StageId + FinalTickIndex`로 제한한다.
- `StageSessionState`는 `StageId`, `CurrentTickIndex`, terminal lifecycle state만 소유한다. `StageSessionTracker`는 scene load마다 새로 생성되어 한 번만 사용되는 scene-local owner이며, single terminal completion emission을 중재한다.
- Terminal Stage Clear synchronization은 `TerminalSessionToken`으로 correlation하고 victory iris의 `BlackReached` 경계에서 matching terminal gate를 release한 뒤 StageResult 또는 GameClear로 진행한다.
- Tile/button presentation barrier는 terminal synchronization과 분리된 presentation mechanism이며, active button visibility/cue 지연은 `PresentationBarrierKey.ButtonActivated`를 사용한다.
- Objective progression은 `StageObjectiveTracker` / `StageObjectiveTickResult`가 소유하며 `StageSessionState`에 duplicate snapshot을 저장하지 않는다. Transient typed stage-run 및 completion-attempt identity는 더 이상 active gameplay/session/completion runtime public API가 아니다.
- UI/presentation projection은 `MinimalStageCompletionReadModel = StageId + FinalTickIndex + ContinueRequest + RetryRequest + NextStageRequest`다. Display identity는 builder에서 validation하지만 completion read model에 저장하지 않는다.
- Retired reward/evaluation/progression residue fields must not re-enter save/profile production DTOs.

## Campaign save architecture V2 policy closeout

- [Pre-Release-Save-Baseline-Policy.md](./Pre-Release-Save-Baseline-Policy.md)
  - current canonical truth for first-public `profile.json` schema 2 and `local-launch-state.json` schema 1, unsupported PlayerPrefs campaign compatibility, and exact-scope pre-release QA reset boundary
- [Save-Architecture-V2-Phase4-Policy-Closeout.md](./Save-Architecture-V2-Phase4-Policy-Closeout.md)
  - historical phase-close provenance; it is not current save compatibility policy
- [Steam-Cloud-File-Inventory-Policy.md](./Steam-Cloud-File-Inventory-Policy.md)
  - current supporting truth for Steam Release Phase B Cloud inventory policy, Auto-Cloud defer status, future exact `profile.json` include rule, Cloud/SteamPipe exclusions, Company/Product path guard, and no-Steam-API guard
- [Campaign-Save-Rollback-Retention-Policy.md](./Campaign-Save-Rollback-Retention-Policy.md)
  - historical superseded rollback/retention provenance; no production rollback or retention obligation remains
- [Campaign-LocalState-Launch-State.md](./Campaign-LocalState-Launch-State.md)
  - current supporting truth for committed LocalState active ownership and active commit point, application-session pending handoff, scene-local running context, matching-token failure policy, restart reset, and DirectPlay exception
- [Campaign-Stage-Sequence-Authority.md](./Campaign-Stage-Sequence-Authority.md)
  - active Phase 3 contract for physical sequence SSOT, catalog eligibility coverage, CI/prebuild validation, and save compatibility ownership
- [Product-Achievement-Foundation.md](./Product-Achievement-Foundation.md)
  - current supporting truth for product-global achievement identity/ledger ownership, durable normal Campaign completion receipt provenance, canonical Saves-root composition, and deferred Gameplay/Steam integration boundaries

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
