# Enemy AI Summon Presentation / Audio / VFX Parity Plan

## 1. Decision Summary

- This is a presentation/audio/VFX parity plan only.
- Option B compile skeleton exists: `EnemyBehaviorModuleKey.Summon`, `EnemySummonBehaviorModuleAsset`, and a fixed typed Summon runtime config slot are implemented.
- Mutable Summon behavior state, runtime emission, and full presentation/audio/VFX parity are implemented for the test-local Behavior Summon path; production asset migration is not implemented.
- Utility `SummonMinion` remains in the Utility capability lane.
- Presentation/audio/VFX runtime contracts are preserved by this document.
- Presentation prefabs, audio definitions, audio bindings, VFX assets, and production assets are not changed by this document.
- Replay/export-visible names, `DeterminismHashBuilder`, `TickPipeline`, `EntitySpawnRequest`, and `EntitySpawnMaterializer` are not changed by this document.
- Initial Option B preserves current presentation/audio/VFX names and cue semantics.
- Neutral naming requires an explicit presentation/audio/VFX asset or schema migration.
- Full lane was not run for this plan or the parity slice.

## 2. Current Presentation / Audio / VFX Surface Inventory

| Surface | Current Name/Type | Owner | Source Data | Presentation-visible? | Audio-visible? | VFX-visible? | Future Policy Candidate | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Summon windup warning | `TickSummonWindupWarningSignal` | `TickResultBuilder.BuildEnemyUtilityWindupPresentation` | final `EnemyUtilityEffectState` for `SummonMinion`, source entity state, effect index, activation sequence, windup ticks, topology, facing, presentation seed | Yes | Yes | Yes | BehaviorEmitterSameSignal | Future Behavior Summon must produce equivalent timing/source facts so the same signal remains available. |
| Summon utility windup phase | `TickEnemyUtilityPresentationSignal(SummonMinion, WindupStarted)` | `TickResultBuilder.BuildEnemyUtilityWindupPresentation` | source entity id, effect index, activation sequence, windup start/end | Yes | Indirect | Indirect | PreserveWithAlias | Used by enemy view presentation state, animator utility windup trigger, and utility scale pulse. |
| Summon utility recover phase | `TickEnemyUtilityPresentationSignal(SummonMinion, RecoverStarted)` | `TickResultBuilder.BuildEnemyUtilityWindupPresentation` | source entity id, effect index, activation sequence, recover start/end | Yes | Indirect | No direct current cue | PreserveWithAlias | Keep as compatibility presentation state unless a separate neutral phase migration exists. |
| Summon phase state | `TickEnemyUtilityPhasePresentationState(SummonMinion, phase)` | `TickResultBuilder.BuildEnemyUtilityWindupPresentation` | final Utility phase, elapsed/duration ticks, effect index, activation sequence | Yes | Indirect | No direct current cue | PreserveWithAlias | Current view state maps Utility phase to source summoner presentation. |
| Summoned child binding | `TickSummonedEnemyPresentationBinding` | `TickResultBuilder.BuildSummonedEnemyPresentationBindings` | final `SummonedEntityState`, final entity, `EnemyDefinitionBindingState`, source entity id | Yes | Yes | Yes | SpawnSeamDerived | Must remain derived from committed child metadata, not from live behavior internals. |
| Summoned enemy view | `SummonedEnemyPresentationResolver` | gameplay host presentation | `TickSummonedEnemyPresentationBinding`, final entity list, `EnemyPresentationArchetypeRegistry` | Yes | No | No | Preserve | Instantiates missing enemy views after summoned binding exists. |
| Spawn visibility change | `TickVisibilityChange(Spawn)` | `TickResultBuilder.BuildAttackPresentation` | attack resolved `FinalizationOperationKind.SpawnEntity`, spawned entity id/cell/facing, post-attack topology | Yes | Yes | Yes | SpawnSeamDerived | Timing/order must remain tied to committed spawn operations. |
| Source summoner windup audio | `EnemyAudioCue.Windup` | `EnemyAudioRequestPlanner.BuildSummonRequests` | `SummonWindupWarnings` where `TickIndex == WindupStartTick` | No | Yes | No | Preserve | Owner is the source summoner entity. Missing inactive owner view is a no-op in `EnemyAudioPresentationController`. |
| Source summoner active summon audio | `EnemyAudioCue.Active` | `EnemyAudioRequestPlanner.BuildSummonRequests` | spawn `VisibilityChanges` plus `SummonedEnemyPresentationBindings.SourceEntityId` lookup | No | Yes | No | Preserve | Owner is the source summoner, not the spawned child. |
| Persistent windup VFX | `EnemyVfxCue.UtilityWindup` | `GameplayVfxPlanning` | `SummonWindupWarnings`, source entity id, effect index, activation sequence, source cell/topology | No | No | Yes | Preserve | Persistent key uses cue, entity anchor kind, source entity id, effect index, and activation sequence. |
| One-shot summon spawn VFX | `EnemyVfxCue.UtilitySummonSpawn` | `GameplayVfxPlanning` | spawn `VisibilityChanges` for entities with summoned enemy binding | No | No | Yes | Preserve | Anchored at spawned cell; seed includes tick, spawned entity id, cue, cell, and topology. |
| Legacy windup presenter cleanup | `GameplayUtilityWindupVfxPresenter.RefreshSummonWarnings` | gameplay host presentation | currently called with an empty signal list | No | No | Legacy cleanup only | InternalOnly | Old direct warning spawning is disabled; canonical playback is Gameplay VFX `UtilityWindup`. |
| Summon presentation seed | `TickSummonWindupWarningSignal.PresentationSeed` | `TickResultBuilder.BuildUtilityWarningPresentationSeed` | tick, source entity id, effect index, source cell, activation sequence | Yes | No | Yes | Preserve | Used for stable persistent windup VFX planning. |
| Source metadata | `SourceEntityId`, `EffectIndex`, `SourceEffectIndex`, `ActivationSequence` | Utility state, request source, summoned child metadata | source entity id, Utility effect index, activation sequence | Yes | Yes | Yes | PreserveWithAlias | Initial Option B may map behavior source slot into compatibility `EffectIndex`/`SourceEffectIndex`. |
| Request snapshot metadata | `EntitySpawnRequestSource` | spawn seam | source entity id, source effect index, trigger tick, origin cell, source facing, source team id | Indirect | Indirect | Indirect | Preserve | Request has no allocated entity id and must remain captured metadata, not a live source reference. |
| Spawn index/order | `EntitySpawnRequest.SpawnIndex`, materializer received order | `EnemyUtilityResolver`, `EntitySpawnMaterializer` | trigger order, spawn index, reserved cells, materialization success | Indirect | Indirect | Yes through spawn order | Preserve | Same-tick multi-summoner ordering characterization already covers this risk. |
| Summon UI/HUD notification | None found | Not applicable | No current Summon-specific HUD mapped output found in UIAccess/HUD scan | No | No | No | NotApplicable | Adding HUD output would require separate UI mapped contract and is not part of initial Option B. |

## 3. Preserved vs Migration-Required Names

| Name/Signal/Cue | Current Meaning | Future Desired Meaning | Policy | Compatibility Risk | Required Evidence |
| --- | --- | --- | --- | --- | --- |
| `TickSummonWindupWarningSignal` | Source summoner has an active Summon windup warning | Same presentation fact, even if source timing owner moves to Behavior | Preserve | High | Windup warning signal parity baseline and future Behavior parity test. |
| `TickSummonedEnemyPresentationBinding` | Spawned enemy child needs archetype view binding | Same committed child binding fact | Preserve | High | Binding parity and spawned archetype view tests. |
| `TickVisibilityChange(Spawn)` | Entity became presentation-visible due to spawn | Same spawn visibility fact | Preserve | High | Spawn visibility change parity and same-tick ordering tests. |
| `EnemyAudioCue.Windup` | Source summoner windup cue | Same source summoner windup cue | Preserve | Medium | Audio windup cue parity with owner view lookup behavior. |
| `EnemyAudioCue.Active` | Source summoner active summon cue | Same source summoner active summon cue | Preserve | Medium | Audio active cue parity from committed spawn visibility/binding. |
| `EnemyVfxCue.UtilityWindup` | Persistent source-attached Summon windup warning VFX | Same cue name for initial Option B | Preserve | High | VFX persistent desired-state parity and cleanup parity. |
| `EnemyVfxCue.UtilitySummonSpawn` | One-shot spawned-cell summon VFX | Same cue name for initial Option B | Preserve | High | Spawn VFX parity and failed-placement no-VFX tests. |
| `EffectIndex` / `SourceEffectIndex` | Utility effect slot/source metadata | Compatibility source slot metadata | PreserveWithAlias | High | Source metadata parity and replay/export compatibility evidence. |
| future neutral windup signal name | None today | Optional neutral presentation name | RenameRequiresAssetMigration | High | Explicit presentation schema migration and dual-label/rename tests. |
| future neutral audio cue names | None today | Optional Summon-specific audio cue names | RenameRequiresAssetMigration | High | Audio profile/binding migration and parity tests. |
| future neutral VFX cue names | None today | Optional neutral Summon VFX cue names | RenameRequiresAssetMigration | High | VFX map/binding/prefab migration and parity tests. |
| future Behavior source vocabulary | None externally today | Internal behavior source key/index/slot | InternalOnly | Medium | Adapter tests proving external compatibility names remain stable. |

Initial Option B preserves current presentation/audio/VFX names and cue semantics. Neutral naming requires explicit asset/schema migration. Do not rename `UtilityWindup`, `UtilitySummonSpawn`, windup audio semantics, or active summon audio semantics in initial Option B.

## 4. Future Behavior Summon Presentation Policy

Signal source policy:

- Future Behavior Summon must emit or expose timing/source facts sufficient for `TickResultBuilder` to build the same presentation facts.
- `TickSummonWindupWarningSignal` remains the external windup warning surface for initial Option B.
- `TickSummonedEnemyPresentationBinding` remains derived from committed summoned child metadata in the final authoritative snapshot.
- `TickVisibilityChange(Spawn)` remains derived from spawn finalization operations.
- `TickEnemyUtilityPresentationSignal(SummonMinion, ...)` and `TickEnemyUtilityPhasePresentationState(SummonMinion, ...)` may remain as compatibility presentation aliases during initial Option B if source view animation/pulse paths still consume them.

Behavior boundary:

- `SummonBehaviorRuntime` owns timing/state/request emission only after it actually exists.
- It must not play audio, spawn VFX, instantiate views, write `WorldState`, allocate entity ids, or calculate placement legality directly.
- It must not carry presentation prefab, audio definition, audio binding, or VFX asset references in initial Option B.

Spawn seam dependency:

- `EntitySpawnRequest` remains captured request snapshot metadata and still has no allocated entity id.
- `EntitySpawnMaterializer` remains placement/materialization/id allocation owner and preserves received request order.
- `FinalizationBatch.SpawnEntity` remains the authoritative write path.
- Successful materialization must still write `SummonedEntityState` and `EnemyDefinitionBindingState` for presentation binding and downstream compatibility.

Metadata required:

- `SourceEntityId`.
- Compatibility source slot mapped to current `EffectIndex` / `SourceEffectIndex`.
- `TriggerTick`.
- `SpawnIndex`.
- `OriginCell`.
- `SourceFacing`.
- `SourceTeamId`.
- `ActivationSequence`.
- Windup and recover start/end ticks.
- Presentation seed inputs equivalent to current source/tick/effect/cell/activation data.

## 5. Audio Compatibility Policy

| Audio Cue | Current Trigger | Current Owner | Source Binding | Future Policy | Risk | Required Test |
| --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioCue.Windup` | `SummonWindupWarnings` where current tick equals windup start | `EnemyAudioRequestPlanner` then `EnemyAudioPresentationController` | source summoner view; attached if binding has an attachment slot, otherwise 2D | Preserve | Missing signal would remove windup cue after Behavior migration | `BehaviorSummon_AudioWindupCueParity`. |
| `EnemyAudioCue.Active` | spawn visibility change for entity with summoned binding source | `EnemyAudioRequestPlanner` then `EnemyAudioPresentationController` | source summoner view, resolved from binding `SourceEntityId` | Preserve | Binding/source drift can move cue to child or drop cue | `BehaviorSummon_AudioActiveSummonCueParity`. |
| Missing owner view behavior | live owner lookup fails | `EnemyAudioPresentationController.TryResolveLiveOwner` | no playback when owner view missing/inactive | Preserve | Behavior migration could accidentally create fallback playback | `BehaviorSummon_OwnerViewMissing_AudioFallbackParity`. |
| same-tick multi-summoner cue order | planner iteration over presentation lists | request planner / pending plan order | source owners in presentation order | Preserve | Request order or binding order drift changes audible ordering | `BehaviorSummon_SameTickMultiSummonerPresentationOrderParity`. |

Policy:

- Initial Option B preserves current audio cue semantics.
- If cue key names include Utility through authoring/profile context, keep them as compatibility aliases initially.
- Do not require new audio definition or binding assets for initial Option B unless a separate parity plan revision proves a need.
- Source summoner cue owner remains the source entity/view, not the spawned child.
- Failed placement and max-alive blocked paths must not emit the active summon cue because no committed spawn visibility/binding fact exists.

## 6. VFX Compatibility Policy

| VFX Cue | Current Trigger | Current Owner | Position Source | Future Policy | Risk | Required Test |
| --- | --- | --- | --- | --- | --- | --- |
| `EnemyVfxCue.UtilityWindup` | each current `TickSummonWindupWarningSignal` not suppressed by same-tick source exit | `GameplayVfxPlanning` / Gameplay VFX persistent desired state | source entity center with source cell/topology fallback | Preserve | Lost windup signal or changed persistent key can leak or drop VFX | `BehaviorSummon_UtilityWindupVfxParity`. |
| `EnemyVfxCue.UtilitySummonSpawn` | spawn `TickVisibilityChange` for summoned enemy binding | `GameplayVfxPlanning` one-shot request | spawned cell/topology from visibility change | Preserve | Cue rename or materialization timing drift changes one-shot VFX | `BehaviorSummon_UtilitySummonSpawnVfxParity`. |
| old direct windup presenter | cleanup-only empty refresh | `GameplayUtilityWindupVfxPresenter` | none | InternalOnly | Re-enabling old path would duplicate VFX | `BehaviorSummon_PresentationDoesNotMutateAuthoritativeState` plus VFX duplicate guard. |

Policy:

- Initial Option B preserves `UtilityWindup` and `UtilitySummonSpawn` cue names.
- Neutral cue names require explicit VFX asset/map/binding migration.
- Spawn VFX remains derived from committed spawn visibility and summoned binding facts.
- Windup VFX remains derived from behavior timing/windup presentation facts.
- Failed placement and max-alive blocked paths must not emit `UtilitySummonSpawn` because no committed spawn visibility/binding fact exists.
- Source death/cancel/topology suspend must remove the windup warning desired state through absent/suppressed warning facts.

## 7. Baseline Capture Plan

| Baseline | Scenario | Captured Artifacts | Used For | Required Before Option B? |
| --- | --- | --- | --- | --- |
| Utility Summon windup warning | one source enters Summon windup | stage/setup seed, tick range, warning list, source entity id, effect index, activation sequence, windup ticks, presentation seed | Windup signal and VFX desired-state parity | Yes |
| Utility Summon successful spawn binding | one successful spawn | spawned entity id, spawn index, spawn cell, summoned binding, archetype binding, source entity id | View binding and active audio/VFX parity | Yes |
| Utility Summon spawn visibility | one successful spawn | `TickVisibilityChange(Spawn)`, topology, facing, order among other changes | Spawn visibility/order parity | Yes |
| Utility Summon audio windup cue | windup start tick | enemy audio request plan, owner entity id, cue, binding result if exposed | Windup audio parity | Yes |
| Utility Summon active cue | successful committed spawn | enemy audio request plan, source owner, spawned binding lookup | Active summon audio parity | Yes |
| `UtilityWindup` persistent VFX | active windup across ticks | VFX request plan, persistent key, seed, source anchor/fallback | Persistent VFX lifecycle parity | Yes |
| `UtilitySummonSpawn` one-shot VFX | successful committed spawn | VFX request plan, seed, spawned cell/topology, spawned entity id | Spawn VFX parity | Yes |
| same-tick multi-summoner ordering | two sources commit on same tick | presentation signal order, audio request order, VFX request order, spawned ids, spawn indexes | Cross-source deterministic parity | Yes |
| failed placement no-spawn | no candidate cell | `SummonSkipped`, no id allocation, no visibility spawn, no active cue, no spawn VFX | Failure parity | Yes |
| max-alive blocked no-spawn | max alive gate blocks | `SummonSkipped MaxAliveReached`, no visibility spawn, no active cue, no spawn VFX | Max-alive parity | Yes |
| source death/cancel cleanup | source invalid/canceled during windup | warning absence/cancel signal, persistent VFX stop, no active cue | Cancellation parity | Yes |
| topology participation suspend/cancel | source leaves bottom face/topology participation | warning phase timing, suspended/canceled presentation facts, VFX desired state | Topology parity | Yes |
| owner view missing | source view missing/inactive during cue | audio request plan and no-op playback behavior | Audio fallback/no-op parity | Yes |

## 8. Compatibility Options

| Option | Pros | Cons | Presentation Risk | Audio/VFX Risk | Recommendation |
| --- | --- | --- | --- | --- | --- |
| A. Preserve current presentation/audio/VFX names and cue semantics | Lowest drift; aligns with replay/export preserve policy; avoids asset migration | Utility-flavored names remain visible | Low | Low | Selected for initial Option B. |
| B. Dual-label compatibility period | Allows gradual neutral naming | Requires versioned presentation/audio/VFX policy and duplicate surface handling | Medium | Medium | Defer until a separate asset/schema migration is approved. |
| C. Rename to neutral Behavior names immediately | Cleaner future vocabulary | Breaks current bindings, tests, authoring maps, and trace expectations | High | High | Reject for initial Option B. |

Decision:

- Initial Option B uses Option A.
- Defer neutral names to explicit presentation/audio/VFX asset migration.
- Do not rename `UtilityWindup` or `UtilitySummonSpawn` in initial Option B.
- Do not rename current windup or active summon audio cue semantics in initial Option B.

## 9. Future Test Matrix

| Test | Current/Future/Migration | Purpose | Required Before Option B Implementation? |
| --- | --- | --- | --- |
| same-tick multi-summoner ordering | Current already covered | Baseline request/id allocation order | Already recorded |
| request payload snapshot | Current already covered | Captured request metadata does not drift | Already recorded |
| Utility Summon replay | Current already covered | Baseline replay contract | Already recorded |
| UtilityArchetypeSummon replay | Current already covered | Baseline archetype binding replay contract | Already recorded |
| duplicate Utility/Behavior Summon guard design | Current already covered | Prevent double authoring | Already recorded as design |
| runtime state shape design | Current already covered | Future state ownership boundary | Already recorded as design |
| asset migration plan | Current already covered | Asset-scoped migration boundary | Already recorded as design |
| replay/export compatibility plan | Current already covered | External name/hash/export compatibility | Already recorded as design |
| `BehaviorSummon_WindupWarningSignalParity` | Current | Preserve windup warning facts | Implemented |
| `BehaviorSummon_SummonedEnemyPresentationBindingParity` | Current | Preserve child binding facts | Implemented |
| `BehaviorSummon_SpawnVisibilityChangeParity` | Current | Preserve spawn visibility timing/order | Implemented |
| `BehaviorSummon_AudioWindupCueParity` | Current | Preserve source windup audio cue | Implemented |
| `BehaviorSummon_AudioActiveSummonCueParity` | Current | Preserve source active summon cue | Implemented |
| `BehaviorSummon_UtilityWindupVfxParity` | Current | Preserve persistent windup VFX | Implemented |
| `BehaviorSummon_UtilitySummonSpawnVfxParity` | Current | Preserve one-shot spawn VFX | Implemented |
| `BehaviorSummon_SameTickMultiSummonerPresentationOrderParity` | Current | Preserve presentation/audio/VFX order | Implemented |
| `BehaviorSummon_FailedPlacement_NoSpawnVfxOrActiveCue` | Current | No committed spawn side effects on failed placement | Implemented |
| `BehaviorSummon_MaxAliveBlocked_NoSpawnVfxOrActiveCue` | Current | No spawn cue when max alive blocks | Implemented |
| `BehaviorSummon_SourceDeathCancelsWindupPresentation` | Current | Windup cleanup on invalid source | Implemented |
| `BehaviorSummon_TopologySuspendPresentationParity` | Current | Topology suspend/cancel presentation parity | Implemented |
| `BehaviorSummon_OwnerViewMissing_AudioFallbackParity` | Current | Preserve missing owner no-op behavior | Implemented |
| `BehaviorSummon_PresentationDoesNotMutateAuthoritativeState` | Current | Guard presentation-only boundary | Implemented |
| `MigratedSummon_PresentationBaselineParity` | Migration | Compare migrated presentation facts to Utility baseline | Before migration close |
| `MigratedSummon_AudioBaselineParity` | Migration | Compare migrated audio request/playback plan to Utility baseline | Before migration close |
| `MigratedSummon_VfxBaselineParity` | Migration | Compare migrated VFX request/lifecycle plan to Utility baseline | Before migration close |
| `MigratedSummon_NoUtilityAssetResidueButCueNamesPreserved` | Migration | Ensure asset source moved while compatibility cue names remain | Before migration close |
| `MigratedSummon_NeutralCueNamesRequireAssetMigration_IfIntroduced` | Migration | Prevent unversioned cue rename | Before neutral rename |
| `GravityFieldAura_PresentationUnaffected` | Migration | Guard non-goal Utility effect presentation | Before migration close |
| `RetiredLockNearbyBoxes_PresentationGuardUnaffected` | Migration | Guard retired compatibility path | Before migration close |

## 10. Dependencies

- Spawn/entity creation seam: [Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md](./Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md).
- Duplicate Utility/Behavior Summon guard: [Enemy-AI-Summon-Duplicate-Guard-Design.md](./Enemy-AI-Summon-Duplicate-Guard-Design.md).
- Future runtime state shape: [Enemy-AI-Summon-Behavior-Runtime-State-Design.md](./Enemy-AI-Summon-Behavior-Runtime-State-Design.md).
- Asset migration plan: [Enemy-AI-Summon-Asset-Migration-Plan.md](./Enemy-AI-Summon-Asset-Migration-Plan.md).
- Replay/export compatibility plan: [Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md](./Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md).
- Full lane / CI release gate policy.

## 11. Explicit Non-Goals

- No production `EnemySummonBehaviorModuleAsset`.
- No production Utility SummonMinion migration.
- No Utility `SummonMinion` production asset migration.
- No production asset changes.
- No presentation/audio/VFX asset migration.
- No presentation prefab change.
- No audio definition or audio binding asset change.
- No VFX asset change.
- No replay/export-visible rename.
- No `DeterminismHashBuilder` runtime behavior change.
- No `TickPipeline` runtime behavior change.
- No `EntitySpawnRequest` or `EntitySpawnMaterializer` contract change.
- No Utility whole-lane migration.
- No `GravityFieldAura` changes.
- No `RetiredLockNearbyBoxes` changes.
- No `EnemyBehaviorRuntimeSet` generic registry.
- No `logicModuleAssets`.
- No direct `WorldState` spawn writes.
- No new Summon UI/HUD notification surface.

## 12. Option B Entry Criteria

- Presentation/audio/VFX parity plan accepted.
- Replay/export compatibility plan accepted.
- Asset migration plan accepted.
- Duplicate Utility/Behavior Summon guard accepted.
- Runtime state design accepted.
- Behavior emitter request ordering contract accepted.
- Source metadata vocabulary accepted without Utility-only coupling.
- Option B implementation slicing and validation gates accepted; see [Enemy-AI-Summon-Option-B-Implementation-Plan.md](./Enemy-AI-Summon-Option-B-Implementation-Plan.md).
- Full lane / CI release gate policy decided.

Full presentation/audio/VFX parity is implemented for the test-local Behavior Summon runtime/emitter path. Current names and cue semantics are preserved: `TickSummonWindupWarningSignal`, `TickSummonedEnemyPresentationBinding`, `TickVisibilityChange(Spawn)`, `EnemyAudioCue.Windup`, `EnemyAudioCue.Active`, `UtilityWindup`, and `UtilitySummonSpawn` remain unchanged. Production asset migration has not started. Neutral naming remains future asset/schema migration only. Full lane was not run unless explicitly reported.
