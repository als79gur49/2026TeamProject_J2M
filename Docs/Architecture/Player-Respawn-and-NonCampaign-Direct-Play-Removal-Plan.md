# Player Respawn and NonCampaign Direct Play Removal Plan

Status: completed on 2026-09-24. All four stages passed their automated
gates and read-only stage-end reviews. The player in-world respawn path,
NonCampaign Direct Play entry, and `Respawn` execution-stage member are retired.
This document retains the pre-removal inventory and validation history.
For current death and recovery behavior, see
[Gameplay Death Recovery Lifecycle](./Gameplay-Death-Recovery-Lifecycle.md).

## Decision and scope

- Campaign death keeps its chance commit and retry or level-failed terminal flow.
- Player in-world recreation after death is to be removed.
- NonCampaign Direct Play is to be removed from editor, capture, and runtime entry paths.
- MoonBlock generator respawn remains a separate gameplay feature.
- Death VFX, death direction hints, and retained death pose are presentation concerns.
  Removing player respawn does not imply removing these effects.
- A stage scene opened without an active campaign slot rejects entry before gameplay starts.
- Bare `--capture-stage` uses a validated temporary campaign slot. Stage, campaign
  sequence membership, and its level group are checked before persistence changes.
- The Goal includes retirement of the `Respawn` phase name and enum member.
  The MoonBlock generator remains after Cleanup under a more precise phase name.

## Why death pose retention was changed first

Before the preparatory change, an active `PlayerRespawnDelayRecord` supplied the
`PlayerDeathHoldSignals` used to retain the removed player's pose. Deleting the
player respawn processor at that point could also remove the hold signal during
campaign terminal playback. The presentation builder now starts a hold from a
`DidDieThisTick` player death signal when no delay record exists, while an
existing delay record retains its timing metadata and does not create a
duplicate hold. The track planner also captures a pose directly from the
death signal.

At Stage 1, this was a dependency reduction before player respawn removal.
The processor still created delay records and could call `SpawnEntity` then.
The fallback covered death signals from accepted fatal damage and DestroyTile
movement. The subsequent cleanup-death audit and removal are recorded below.

Relevant code: [death signal construction](../../Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs),
[hold construction](../../Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs),
[pose retention](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTrackPlanner.cs), and
[campaign death admission](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs).

## Pre-removal executable paths

This table is the baseline inventory before Stage 2. The completed Stage 2
implementation and evidence are recorded below.

| Path | Pre-removal behavior | Removal implication |
| --- | --- | --- |
| Campaign stage | The stage installer disables player respawn; campaign flow starts its terminal transition on death. | Preserve campaign retry, chance commit, and level-failed publication timing. |
| NonCampaign Direct Play | Editor launch sets a context that suppresses campaign flow. The ordinary host configuration can allow player respawn. | Migrate or retire its entry points before deleting the recovery behavior they exercise. |
| Capture with only `--capture-stage` | The default persistence mode creates a NonCampaign context. | Replace or reject this implicit mode deliberately; update CLI tests and callers. |
| Stage scene opened without an active campaign slot | The installer does not attach campaign flow or disable player respawn. | Connect it to campaign before gameplay or reject it before gameplay begins. |
| MoonBlock generator | A separate processor runs inside the current Respawn phase. | Keep its spawn, blocked facts, and timing when changing the phase contract. |

Evidence: [stage installer](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs),
[editor launcher](../../Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs),
[capture bootstrap](../../Assets/_Features/Stages/Runtime/Load/PlayerCaptureLaunchBootstrap.cs),
the deleted `TickPipeline.RespawnProcessor.cs` (historical source), and
[phase composition](../../Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs).

## Pre-implementation inventory (2026-09-24)

This is an investigation checklist, not step-1 acceptance evidence. Cleanup removes
`hp <= 0` and `markedForDeath` candidates, and its `RemovedEntityIds` does not
distinguish death from other removal. The table records the current source of
proof and the remaining check before a new death signal may be emitted.

| Candidate | Current authoritative evidence | Current signal / open check |
| --- | --- | --- |
| Accepted fatal attack damage, including delayed and passive contact damage | Accepted `DamageResolution`, post-attack HP and cleanup removal | `BuildPlayerDeathPresentation` emits a player signal; verify overlapping hits produce exactly one and carry the selected source direction. |
| DestroyTile movement | Accepted movement `MarkDestroy` with `BoundaryReason=DestroyTile`, followed by cleanup removal | Movement-owned signal uses facing fallback; verify a real pipeline result. |
| DestroyTile activation under occupant | The activation query can target a player-shaped Unit, but needs an inactive-to-active topology transition and a player still occupying that cell | Ordinary topology rotation is owned by the moving player and relocates that player to the new bottom face; no valid single-player runtime case of a stationary player under a newly active tile was found. Keep generic activation facts without treating the query alone as player-death reachability. |
| Other movement destroy | Item consumption, barricade/impact box destruction and `DestroySelf` have separate movement paths | Prove target type restrictions and any player-reachable path before classifying a player removal. |
| Player already at zero HP or marked before `RunTick` | Tick-start snapshot and cleanup candidate | No attack or DestroyTile signal is guaranteed. Determine whether production bootstrap or a live gameplay writer can create each state; a test-only write is not reachability proof. |
| Arbitrary direct removal | `WorldState` write-context removal bypasses cleanup's death provenance | Do not infer death from disappearance; identify any production caller and classify its purpose. |

The current non-test writer search finds `ApplyDamage` in attack commit/batch
paths, `MarkDestroy` in attack/movement/tile batch paths and the MoonBlock
processor, and `RemoveEntity` in cleanup and the MoonBlock processor. The
MoonBlock processor targets its configured block or a blocking box. Stage
authoring validation rejects `Hp <= 0`, and `StageRuntimeBuilder` creates its
player with that positive HP and no destroy mark. This is source evidence
against a production source-less player cleanup death at stage bootstrap;
step 1 still needs executable coverage for valid attack and tile routes and
for invalid/test-only removal that must not raise a false death signal.

Existing coverage has a synthetic presentation-builder case for multiple
accepted hits choosing one fatal source and another case where a rejected hit
plus cleanup removal emits no death. `TileFeatureEffectResolverTests` has a
real pipeline case for a scripted player move onto DestroyTile. Its activation
tests cover a generic ground unit, not a player death signal. Tile entity
operations are appended to `MovementPhaseResult.ResolvedOperations`, so the
builder could consume such an operation. However, the activation query requires
an inactive-to-active topology transition under an occupant, and
`SurfaceTraversalQueries.TryResolveBottomFaceRotation` relocates the rotating
player to the new bottom face. A non-player mover has no player traversal
entitlement. The attempted two-unit fixture never rotated topology; it did
not establish a production player activation death. Existing campaign death
tests largely inject a synthetic `TickResult`, which does not prove the
pipeline-to-feed-to-arbiter path required by step 1.

Death VFX is an authored presentation path, not an automatic VFX request for
every player death: `PlayerVfxRequestPlanner` suppresses the same-tick player
damage request when a death signal exists, and the existing migration test
expects no player Death VFX request without explicit authoring. Step 1 must
verify the currently authored death presentation and direction at the host
boundary, then preserve that policy through processor removal rather than
inventing a new implicit VFX cue.

The input host currently blocks player input from `PlayerDeathHoldSignals` and
enters a separate terminal hold after campaign arbitration. Step 1 must cover
the interval inside the death tick and any subsequent tick that a slotless
host can run. The campaign tests currently contain synthetic death results;
they need at least one real `TickPipeline.RunTick` result passed through the
production feed and arbiter.

Affected test inventory includes `CleanupPhaseScenarioTests`,
`AttackPhaseScenarioTests`, `CampaignStageFlowTests`,
`ActualSceneBootstrapSmokePlayModeTests`, `StageLaunchContextOwnershipTests`,
`StageDefaultStageIdPolicyTests`, `PlayerCaptureLaunchBootstrapSafetyTests`,
`SaveSlotValidationAndDirectPlayTests`, `PlayerContinuousLocomotionReplayTests`,
`GameplayTimingPresetTests`, and `FinalizeNoRecheckArchitectureTests`.
`CleanupPhaseScenarioTests` currently contains direct player-respawn tests
in both Core and Extended categories; step 2 must name each retained lower-level
case before step 3 removes or repurposes it.

The lower-level `CleanupPhaseScenarioTests` cases to disposition at step 2 are
`PlayerDiesOnInitialBottomFace_SameBottomRespawnUnchanged`,
`PlayerRespawnTargetActiveFrontFace_RespawnDeferredUntilBottomFace`,
`PlayerDiesOnNonInitialFace_RespawnDeferredUntilTopologyReset`,
`PlayerDiesOnNonInitialFace_RespawnSucceedsAfterTopologyReturnsToInitialFace`,
`DeferredRespawn_DoesNotDuplicateOccupancy`,
`RespawnBlockedAfterTopologyReset_RetriesWithoutTopologyLoop`,
`Respawn_ConfiguredDelay_WaitsEligibleTickBeforeRespawning`,
`Respawn_DefaultCompositionRootDelay_UsesConfiguredDefaultTiming`,
`Respawn_DisabledPolicy_WaitsConfiguredDelayBeforeSuppression`,
`Respawn_TopologyMismatch_DefersOnlyAfterConfiguredDelayElapsed`,
`Respawn_SolidBlockerAtInitialSpawn_SkipsUntilBlockerIsRemoved`,
`Respawn_InactiveFaceSolidAtSpawn_ResetsTopologyBeforeBlockedRetry`,
`Respawn_InactiveFaceDetachedHiddenOccupant_ResetsTopologyThenCommits`, and
`Respawn_PlayerControlState_IsResetWhenPlayerReturns`.
The actual-scene `NonCampaignPlayerDeathRespawnRecreatesPrefabView` case is a
step-2 migration target, not a retained lower-level case. The replay death
fixture currently asserts both `PlayerRespawnDelayStarted` and
`RespawnCommitted` and must change in step 3.

The step-4 source inventory is `TickPhase.cs` (`Respawn = 4`),
`TickPipeline.RunRespawnPhase`, `TickResult.CompletedAllPhases`,
`TickTraceBuilder`, `TickTraceFormatter`, `TickResultBuilder`, and the
`RespawnPhaseResult` carrier. `TickPipelineStructureSimulationTests` and
`TickResultOwnershipCoreTests` cover parts of the five-stage shape. No hash
comparison alone proves phase labels or MoonBlock presentation facts, so the
step-4 gate needs direct assertions for those fields.

Capture argument inventory: `PlayerCaptureLaunchOptions` accepts
`--capture-stage value`, `--capture-stage=value`, and `-captureStage value`
(it also parses `-captureStage=value`). The current bootstrap's default mode
calls `PrimeNonCampaignCapture`; the temp-slot branch writes `level-01`
without consulting the sequence. The production sequence asset groups
`stage-0-1` through `stage-0-3` under `level-0` and `stage-1-*` under
`level-1`. Existing tests cover normalization of the split long form, bare
inline long-form NonCampaign bootstrap, and explicit temp-slot chances, but
do not prove all three spellings use the selected replacement policy or
preserve persistence and context on invalid stage input.

Scene test migration inventory: the VFX actual-scene bootstrap tests use
`stage-0-1` and `stage-1-1`, so a validated campaign fixture can preserve
their runtime-root purpose. BGM PlayMode tests use `stage-0-1`, `stage-0-2`,
`stage-0-3`, and `legacy-stage-5-1`; the first three are sequence stages,
while the legacy stage is catalog-only and its stop-BGM assertion needs a
content-level seam or another valid campaign stage with the same authored
BGM behavior. The current `_Audio.asset` inventory shows the legacy stage is
the only listed stage with a null gameplay BGM profile, so the stop-BGM case
currently needs a content-level seam; silently replacing it with a sequence
stage would stop testing the same condition. UI tests that prime a stale
NonCampaign context are recovery
negative cases; they should use a raw retired value-1 fixture rather than a
supported `CreateNonCampaign` factory. The real-scene player respawn smoke
must become a campaign death/terminal test in step 2.

Persisted-context inventory: `EditorDirectPlayContextStore` schema 2 reads a
raw mode through `Enum.IsDefined`, so value `1` currently restores a live
NonCampaign context. Its public constructor, `SetCurrent`, and `ForStage` do
not reject a campaign mode with `SuppressCampaignFlow=true`.
`StageEditorSessionStores` binds that JSON to `SessionState` and separately
persists ownership mode and pending launch fields. The ownership record's
`IsValid` also accepts mode `1`; transition failure recovery can restore a
captured direct-play context when ownership generation still matches. Existing
ownership tests cover many exact-match cleanup cases, but do not directly
exercise schema-2 SessionState JSON with retired mode `1` or a supported
mode carrying the suppression flag. Step 2 must cover both, including the
case where a newer owner must be preserved while a stale entry is cleared.

Step-3 wiring inventory: `GameplaySceneHostConfiguration` owns
`DisablePlayerRespawn`, `PlayerRespawnTimingSettings`, and its snapshot;
`GameplayHostRuntimeFactory` forwards both policy and delay into
`GameplayCompositionRoot`/`GameplayBootstrapper`/`TickPipeline`, and
`GameplayHostRuntimeContext` and `GameplaySceneHost` expose the delay. The
`GameplaySimulationTimingPreset` serializes `playerRespawnTiming` in
`GameplaySimulationTimingPreset_DefaultShowcase.asset`. The processor feeds
`RespawnPhaseResult` player entities, delay records, placements, and topology
reset; `TickResultBuilder` turns those into spawn visibility, spawn reason,
death-hold timing, and topology motion. `GameplayInputHost` currently derives
an input block from the death-hold list. These are player-recreation consumers
to disposition together, with the death signal and retained pose still
required after their delay metadata is removed.

Shared contracts to preserve or assess separately are the
`MovementExecutionBoundaryKind.SpawnRespawnPlacement` value used by attack
and enemy spawn, `PlayerDeathDisplacementPlanner`'s generic spawn visibility
cleanup, `PlayerActionUseCounterState`'s death-signal reset and initial-spawn
handling, and authored initial-spawn/death VFX. The player-respawn branch in
`PlayerActionUseCounterState` and `GameplayVfxPlanning` can be removed only
after source checks show no other caller; `EntitySpawnPresentationReason`
numeric compatibility needs an explicit decision. `DeterminismHashBuilder`
hashes the event log, so removing campaign delay/suppression lines changes
hash expectations even when the authoritative final world is identical.

## Stage 1 death-contract evidence (2026-09-24)

The production writer audit found two authoritative player-death sources:
accepted fatal `DamageResolution` and accepted DestroyTile `MarkDestroy`
movement operations. The builder requires post-attack HP or cleanup removal
for damage and requires a matching DestroyTile operation plus cleanup removal
for tile death. It deduplicates both sources by player ID. Generic cleanup
removal, direct `RemoveEntity`, and box-only movement destruction are not
promoted to player death. Authoring rejects nonpositive spawn HP, the runtime
builder creates a healthy player, and no live non-test direct player-removal
writer was found. The activation query is broader than the currently reachable
single-player topology path, as recorded in the inventory above.

`FatalPassiveContact_RealPipelineEmitsOneSourcedDeathAndHold` covers a real
accepted fatal attack with attacker direction, a single death signal and
death-hold start. The existing multiple-hit builder case confirms canonical
first-fatal-source selection; the rejected-damage plus cleanup-removal case
confirms no false signal or hold. The existing moving-player DestroyTile
pipeline case covers environmental facing fallback. The new
`PipelineDestroyTileDeathAndObjectiveClear_FeedCommitsDefeatOnceAndBlocksInput`
case carries an actual `RunTick` result through the production feed and
arbiter. It checks one chance decrement, one death count, defeat over a
same-tick clear, no completion receipt, one retry route, death-tick input
clearing, and terminal input hold. Its always-satisfied objective is a test
fixture for arbitration; ordinary stage goals require a live player. The
existing host and VFX tests cover retained pose, direction, and the authored
Death VFX policy without adding an implicit death cue.

Evidence from the Stage 1 implementation: `./run_tests.sh core` passed EditMode
293/0 and PlayMode 113/0. Filtered `full` tests passed the real pipeline
feed case 1/0, real fatal contact case 1/0, pose/direction cases 2/0, and
`PlayerDeath` cluster EditMode 27/0 plus PlayMode 1/0. Logs are under
`/mnt/d/J2M/evidence/player-respawn-removal-2026-09-24/`. The confirmed
no-slot policy requires rejection before gameplay; its installer and
actual-scene assertion belong to Stage 2 and Stage 3.

## Stage 2 entry-removal evidence (2026-09-24)

The editor window, quick menu, Replay Last Stage, and public `LaunchStage`
accept only validated campaign temporary or production slots. The window
lists the catalog and campaign sequence intersection. Runtime context
construction and restore reject retired mode value `1` and campaign contexts
with `SuppressCampaignFlow=true`; exact-match stale SessionState and ownership
cleanup preserves a newer owner. The stage installer rejects an inactive
campaign runtime before host start. Bare capture-stage forms all resolve to a
temporary slot after catalog, sequence, and level-group validation; invalid
legacy stage inputs leave persistence and launch context untouched. The
actual-scene death test now uses campaign fatal contact and checks one chance
spent and terminal input hold. Legacy stage BGM and tile checks use a
content-only seam.

`./run_tests.sh ui` passed EditMode 1615/0, and `./run_tests.sh core` passed
EditMode 293/0 and PlayMode 113/0 with the review additions. Filtered
`full` launcher/capture tests then passed EditMode 83/0, including positive
`LaunchStage` and Replay Last lifecycle execution with the actual PlayMode
flip suppressed by a test hook. VFX and BGM actual-scene tests passed
PlayMode 14/0; two different stage bootstrap scenes passed PlayMode 2/0.
The stage-1-1 actual scene used a real split `--capture-stage` invocation,
ran five ticks, and force-cleared the stage (PlayMode 1/0). The stage-0-1
actual scene used `-captureStage`, produced fatal contact, spent one of the
capture default's two chances, selected `DeathRetry`, and installed a new
stage-0-1 host with a live player (PlayMode 1/0).
The full actual-scene class
bundle hit the Unity 285-second watchdog, so these focused results do not
claim that broader bundle passed. Logs are under
`/mnt/d/J2M/evidence/player-respawn-removal-2026-09-24/`. The editor launcher
API and capture entry points ran through Unity integration tests; a human menu
click and player-build capture smoke have not run in this environment. Those
manual routes are not claimed as tested.

The fourteen lower-level `CleanupPhaseScenarioTests` cases listed above still
exercise player recreation and are explicitly retained only until Stage 3.
The replay fixture expecting `PlayerRespawnDelayStarted` and
`RespawnCommitted` also remains for Stage 3 conversion.

## Removal order and acceptance gates

The confirmed entry policy rejects a stage scene without a campaign slot before
gameplay. Bare `--capture-stage` validates and seeds a temporary campaign slot.
The Goal retires the `Respawn` phase name and enum member. The value `4` needs
an explicit compatibility decision while the MoonBlock generator stays after
Cleanup. These are product/runtime contracts, not test fixture substitutions.

### 1. Complete the player death presentation contract

Inventory accepted fatal damage, DestroyTile movement, and other legitimate
cleanup removals. `CleanupPhaseResult.RemovedEntityIds` does not carry a death
cause, so establish a canonical cause fact or equivalent proof before deriving
new death signals from cleanup. For each confirmed player death, verify one
death fact, one retained pose start, correct source or fallback direction, and
one campaign terminal admission. Preserve the existing Death VFX path. Check
the input gate: it currently reads `PlayerDeathHoldSignals`, whose lifetime
may shrink when delay records disappear. The gate should match terminal and
recovery policy rather
than rely accidentally on the former respawn countdown.

Gate: targeted tests cover deaths with and without a delay record, including
source-less cleanup deaths that are confirmed to be valid runtime cases. No
duplicate signal, false death, or missing terminal transition is accepted.
Verify that simultaneous death and stage clear still awards defeat precedence
and commits the chance change exactly once. Decide the no-slot direct-scene
death policy before relying on campaign terminal admission as a universal gate.
Record a cause matrix for every production-reachable player removal: its
authoritative death cause or proof of non-death, its single presentation death
signal and hold start when applicable, and its campaign result. If a proposed
source-less death class is empty, prove it is unreachable instead of passing a
vacuous test. At least one integration case must carry a real
`TickPipeline.RunTick` result through the campaign feed/arbitration path;
synthetic `TickResult` tests alone do not close this gate. Verify death VFX and
direction without changing their policy, and verify death-tick input blocking
plus terminal input blocking. No-slot follow-up tick behavior is governed by
the recorded policy and must pass before step 3 begins.

### 2. Retire NonCampaign Direct Play entry paths

Update the editor window default and menu shortcuts, launcher mode switch,
stored context handling, capture argument fallback, and tests that construct
`CreateNonCampaign`. The campaign temporary slot is the existing editor and
capture route to examine for replacement. Do not mechanically switch every
test to it: campaign stage validation may reject legacy fixture stages such
as `legacy-stage-5-1`. Identify which tests need a campaign fixture and which
only need a stage-content bootstrap seam. The editor window currently lists
all catalog stages, while campaign launch accepts only sequence stages; filter
or disable unsupported entries. Replay Last Stage remembers only the stage ID
and currently relaunches NonCampaign, so define its new mode and unsupported
last-stage behavior.

Capture arguments accept multiple stage spellings, including `-captureStage`
and `--capture-stage=...`; migrate their documented commands and callers as
well as `--capture-stage`. The temporary capture seeder currently hardcodes
`level-01`; verify its group against the campaign sequence before using that
mode as a default for each stage. Handle persisted NonCampaign mode value `1`,
SessionState JSON, launch ownership, and pending launch context together.
Reserve or reject the retired numeric value rather than reusing it for another
mode. Migrate tests by their intent across Stage, Gameplay, UI, BGM, VFX, and
capture clusters; update the existing editor direct-play operating contract.
Move the real-scene NonCampaign death/respawn smoke to the chosen campaign
death behavior in this step. Keep lower-level player-respawn tests only as an
explicitly inventoried temporary proof of the behavior to remove in step 3;
they must not exercise a supported NonCampaign production entry.

Gate: no supported editor menu or capture invocation enters NonCampaign mode;
stage bootstrap coverage and campaign temporary-slot behavior remain valid.
An actual editor launch, Replay Last Stage, stale-session recovery, and the
affected capture argument forms need manual or integration evidence. Capture
smoke must include stage-specific group, death, retry, and clear behavior.
The public `LaunchStage` API and runtime context restore must reject mode `1`,
not merely hide it from the editor window. Test SessionState JSON, ownership,
and pending launch combinations, preserving any newer owner while clearing a
stale NonCampaign entry. Replay Last Stage must reject an unsupported saved
stage before mutating context or saves. For all three capture-stage spellings,
test the selected policy, rejection errors, and no persistence mutation on an
invalid stage. If temporary campaign is selected, validate catalog/sequence
membership and derive the level group before `ClearAll` or seed writes.
Validate direct-play context construction, `SetCurrent`, restored DTOs, and
stage launch contexts together: a supported mode must not carry
`SuppressCampaignFlow=true` through the public constructor or stale data.
The stage installer must not silently accept such a context and disable
campaign flow.
Update the editor Direct Play contract, supported-stage list, capture CLI
guidance, and smoke checklist on the same revision. The step-2 report must
list any intentionally retained lower-level respawn tests for step 3.

### 3. Remove player in-world recreation and its consumers

Remove the player processor call in `RunRespawnPhase`, its player templates, delay
state, placement/topology reset behavior, `allowPlayerRespawn` and delay
configuration plumbing, and player-only result, presentation, input, and VFX
consumers after checking their remaining callers. Here, player-only means
player-respawn-only; retain player death signals, death holds, and Death VFX.
Retire the step-2 inventory of lower-level player-respawn tests or replace
their assertions with the chosen death contract. Audit directly opened stage
scenes without an active campaign
slot: removing the editor mode alone does not make every such host a campaign
host. If this entry is supported, attach campaign flow before gameplay; if it
is unsupported, reject it before gameplay begins. Verify the selected route,
including death-tick and subsequent input/tick behavior, so a playerless world
cannot continue indefinitely.

Even when player respawn is disabled, its processor emits delay and suppressed
events, and event log entries contribute to the determinism hash. Specify the
expected log/hash and replay fixture changes in this step, not after phase
renaming. Update the canonical tick specification and the `Extended`
architecture tests that assert the current processor source and phase wiring.
The serialized `GameplaySimulationTimingPreset.playerRespawnTiming` field has
a production Showcase asset; state the asset change purpose and inspect it in
the Unity editor. `MovementExecutionBoundaryKind.SpawnRespawnPlacement` also
serves enemy and attack spawns, so keep that shared value unless a separate
compatibility migration proves it can change.
`PlayerDeathDisplacementPlanner` clears a death track on generic Spawn
visibility changes despite its respawn-oriented helper name; keep that
lifecycle cleanup unless all remaining spawn paths are accounted for. If the
`EntitySpawnPresentationReason.PlayerRespawn` enum member is retired, reserve
its numeric value until replay and serialized compatibility are assessed;
remove only its player-respawn cue, not initial-spawn or death VFX cues.
`PlayerActionUseCounterState` also resets on death signals, so preserve that
reset while removing its player-respawn spawn branch.

Gate: no production path can recreate a dead player in the same world; campaign
retry and level-failed outcomes still execute; player death presentation and
MoonBlock generator behavior still pass targeted, core, and actual-scene tests.
The chosen no-slot policy passes a real-scene death test. Approved event-log,
hash, and replay changes are documented, and the affected `Extended`
architecture tests pass on the same revision.
Explicitly update replay tests that expect `PlayerRespawnDelayStarted`,
`RespawnCommitted`, or respawned HP, plus timing preset snapshot tests and the
Showcase timing asset. Report the removed wiring inventory, the before/after
event-log and hash expectations, asset/editor evidence, and focused replay,
`core`, PlayMode, and `Extended` results on the same revision. Include the
`PlayerContinuousLocomotionReplayTests`, `GameplayTimingPresetTests`, and
`FinalizeNoRecheckArchitectureTests` fixtures in the affected test inventory.
Update [Gameplay Death Recovery Lifecycle](./Gameplay-Death-Recovery-Lifecycle.md)
and its architecture index description so they no longer describe executable
player in-world respawn.

### 4. Retire the Respawn phase contract

The phase currently runs both player and MoonBlock processors, then contributes
`TickPhase.Respawn` to the five-phase completion contract. The confirmed policy
retires that member and name after player recreation is removed. Keep numeric
value `4` assigned to the narrower MoonBlock generation phase after reviewing
serialized and replay compatibility. Review phase traces,
`CompletedAllPhases`, replay/hash surfaces, result DTOs, and tests before
changing names. Remove the player-mutation snapshot refresh condition only
after confirming MoonBlock occupancy behavior.

Gate: Cleanup-to-generator execution order, final snapshot, objective facts,
MoonBlock spawn and blocked facts, and replay determinism remain correct.
Test `CompletedAllPhases`, phase trace labels, and MoonBlock generated/blocked
presentation facts directly; the determinism hash does not cover those fields.
The closeout must state the chosen name/enum policy, numeric value `4`
compatibility treatment, trace/result DTO changes, and direct MoonBlock
spawn/blocked evidence. If the Goal includes phase retirement, no remaining
`TickPhase.Respawn` execution or five-phase claim may be counted as completion.

## Stage 3 implementation and evidence (2026-09-24)

The player `RespawnProcessor` source and meta are removed. `TickPipeline` no
longer stores player templates, waits for a delay, resets topology for player
placement, or spawns a replacement player. `RespawnPhaseResult` now carries
only MoonBlock generator logs and facts until Stage 4 renames the phase.
`GameplaySceneHostConfiguration`, timing snapshots/presets, factory,
bootstrapper, host context, and Showcase asset no longer expose player
respawn policy or delay. The Showcase asset change removes only the obsolete
serialized `playerRespawnTiming` field; a Unity Editor test loads and applies
the asset and verifies that serialized field is absent.

The result builder still emits the selected death source, direction, hold, and
retained pose. `GameplayInputHost` stops player commands and further ticks on
a canonical death signal until a new host initializes. The player-only spawn
reason's numeric value `2` is reserved as `ReservedFormerPlayerRespawn`;
initial spawn and authored death VFX stay active. The generic
`SpawnRespawnPlacement` movement boundary remains because enemy and attack
spawns use it. MoonBlock generation still runs after Cleanup.

A previously fatal tick logged `PlayerRespawnDelayStarted`, later
`PlayerRespawnDelayTicking`/`PlayerRespawnDelayElapsed`, and either
`RespawnCommitted`, `RespawnDeferred`, or `RespawnSuppressed`. Those player-only
entries are gone. The event log participates in `DeterminismHashBuilder`, so
hash values change by design for the affected tick sequence; replay equality
on the new code remains the invariant. The `PlayerContinuousLocomotionReplayTests`
fixture now verifies the player stays absent in subsequent snapshots.

Validation on the Stage 3 revision: `./run_tests.sh core` EditMode 293/0 and
PlayMode 113/0; filtered `full` fixtures `CampaignStageFlowTests` 87/0,
`CleanupPhaseScenarioTests` 24/0, `PlayerContinuousLocomotionReplayTests`
18/0, `GameplayTimingPresetTests` 6/0, and
`FinalizeNoRecheckArchitectureTests` 7/0. Actual scene filtered PlayMode
checks each passed 1/0 for no-slot rejection before host initialization,
passive-contact campaign death/retry, and MoonBlock static binding. The
first no-slot run exposed a second UI installer exception after the intended
host rejection; the final test expects both logs and asserts that the host
has no world, input host, or runner. A rejected entry therefore logs two
exceptions while no gameplay starts. The actual editor launch, Replay Last
menu click, and player-build capture smoke were not run; Stage 2 automated
integration evidence covers their underlying launch and bootstrap contracts.
No broad full-lane result is claimed.

Stage 3 removed the inventoried player-respawn-only lower-level tests and
replaced cleanup, boundary, and replay cases with player-absence assertions.
The remaining `PlayerRespawn` references in executable assets are the reserved
spawn-reason tombstone and a negative serialized-field assertion; MoonBlock
`Respawn` names remain intentional feature names. Historical inventory above
records retired names and should not be read as executable paths.

## Stage 4 implementation and evidence (2026-09-24)

The fifth `TickPhase` member is now `MoonBlockGeneration = 4`, with no
`Respawn` alias. Numeric value `4` remains reserved for the same execution
position so ordered phase data keeps its numeric meaning. The runner now calls
`RunMoonBlockGenerationPhase` after Cleanup, appends
`MoonBlockGeneration:Enter` and `MoonBlockGeneration:Exit`, and builds
`MoonBlockGenerationPhaseResult`. `TickResult.CompletedAllPhases` checks this
member. Trace output uses `MoonBlockGeneration.Events`. The MoonBlock
processor, its box spawn and blocked facts, and its existing gameplay event
strings retain their feature vocabulary. The final authoritative snapshot and
objective tracker still run after the generator.

The phase label and trace heading changed, so debug traces and their text
comparisons change by design. `DeterminismHashBuilder` hashes the final state
and event log, not completed phases or trace headings. Stage 4 does not alter
the MoonBlock event log. Filtered `full` evidence: `MoonBlockGeneratorRespawnTests`
16/0 (generated and blocked presentation facts, fifth phase completion, new
trace), `TickPipelineStructureSimulationTests` 2/0 (exact five-stage and trace
order), `FinalizeNoRecheckArchitectureTests` 7/0 (source/spec boundary), and
`TickReplayDeterminismTests` 59/0. Final `./run_tests.sh core` passed
EditMode 293/0 and PlayMode 113/0; final `./run_tests.sh ui` passed EditMode
1615/0. A later targeted fatal passive-contact hold assertion passed 1/0
under filtered `full`; its `core --filter` attempt selected zero tests because
the fixture is outside the lane-preserving core selection. Stage 4 read-only
review found no remaining defect after the Accepted ADR wording was updated.
The actual-scene damage/death VFX port check also passed 1/0 under filtered
`full` on the final code revision.
The 2026-09-16 runtime diagram validation report is historical and now has a
current-contract pointer.

## Validation and reporting

Run focused death, campaign, direct-play, capture, and MoonBlock tests as each
slice changes. Run `./run_tests.sh core` for gameplay changes, affected
PlayMode scene tests for bootstrap/presentation changes, and `./run_tests.sh ui`
if runtime UI changes. Run affected `Extended` architecture tests with a
filtered `full` lane when their exact source/specification assertions change;
`core` alone does not cover them. Record manual editor/capture evidence where a test
cannot exercise the actual entry point. Report touched-cluster results apart
from the documented red full-lane baseline. Keep unrelated working-tree
changes out of each removal slice.
At each step, record executable reference residue separately from intentional
compatibility tombstones, historical documentation, and negative tests.
