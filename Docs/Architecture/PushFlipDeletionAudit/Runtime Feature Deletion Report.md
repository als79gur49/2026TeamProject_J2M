# Runtime Feature Deletion Report

## Push Runtime Branch Reachability

Push runtime branches are reachable from production input and content.

Evidence:

- `PlayerTickCommand.PushPressed` is emitted by `GameplayInputHost`.
- `PlayerControlStateLogic` starts `PlayerActionKind.Push` only for explicit Push commands with a valid target contact.
- `PlayerLogic` suppresses ordinary movement when Push/Flip commands are present, keeping Push out of ordinary move semantics.
- Plain move into a Push box is a no-op/suppression path, not an implicit Push fallback.
- `MovementExpander`, `ImpactReservation`, and finalization paths handle Push slide, impact, blocked, recovery, and disposition outcomes.
- Stage content contains Push-capable boxes in tutorial, showcase, and campaign stage assets.

Classification: `KEEP_CURRENTLY_USED`.

Delete action: none.

## Flip Runtime Branch Reachability

Flip runtime branches are reachable from production input and content.

Evidence:

- `PlayerTickCommand.FlipPressed` is emitted by `GameplayInputHost`.
- `PlayerControlStateLogic` starts `PlayerActionKind.Flip` from explicit Flip commands.
- Flip landing, boundary crossing, blocked landing, hostile impact, `Stay`, `FollowThrough`, and `DestroySelf` dispositions are covered by runtime policy and tests.
- `TickPresentationData` includes flip impact presentation carriers.
- Stage content contains Flip-capable boxes in tutorial, showcase, and campaign stage assets.

Classification: `KEEP_CURRENTLY_USED`.

Delete action: none.

## Box Capability Content Usage

`BoxCapabilities.Push` and `BoxCapabilities.Flip` are production content.

Observed serialized rows under `Assets/_Features/Stages/Content`:

| Capability observation | Count |
| --- | ---: |
| Rows containing Push bit | 1666 |
| Rows containing Flip bit | 1440 |
| Rows with no capability | 1850 |
| Rows with value `27` (`Push|Flip|Destroy|JumpCrushable`) | 1416 |

This means `BoxCapabilities.Push/Flip` cannot be deleted or collapsed without a content migration plan.

Classification: `KEEP_CURRENTLY_USED`.

Delete action: none.

## Impact/Disposition Reachability

| Runtime path | Reachability | Classification |
| --- | --- | --- |
| Push slide | Production input + stage boxes + tests | KEEP_CURRENTLY_USED |
| Push blocked | Production reachable + tests | KEEP_CURRENTLY_USED |
| Push Destroy fallback | Production content has Destroy-capable rows + tests | KEEP_CURRENTLY_USED |
| Push hostile impact target dies/survives | Runtime impact/disposition path + tests | KEEP_CURRENTLY_USED |
| Flip normal landing | Production input + stage boxes + tests | KEEP_CURRENTLY_USED |
| Flip boundary crossing | Runtime topology path + tests | KEEP_CURRENTLY_USED |
| Flip landing blocked | Runtime path + tests | KEEP_CURRENTLY_USED |
| Flip hostile impact target survives -> DestroySelf | Runtime path + tests | KEEP_CURRENTLY_USED |
| Flip hostile impact target dies + landing accepted -> FollowThrough | Runtime path + tests | KEEP_CURRENTLY_USED |
| Flip hostile impact target dies + landing denied -> Stay | Runtime path + tests | KEEP_CURRENTLY_USED |
| Action blocked but recovery emitted | Runtime path + tests | KEEP_CURRENTLY_USED |

## Presentation Reachability

Presentation is active and consumes simulation output.

Evidence:

- `GameplayEntityPresentationApplier` calls `GetComponent<PlayerFlipInteractionDriver>()` and `GetComponent<BoxFlipInteractionDriver>()`.
- Tests instantiate and assert `BoxFlipInteractionDriver` / `PlayerFlipInteractionDriver` behavior.
- VFX governance tests specifically keep the original-view motion driver separate from VFX spawners.
- The presentation path consumes `TickPresentationData`/motion tracks; no evidence was found that these drivers directly mutate authoritative simulation.

Classification: `KEEP_CURRENTLY_USED`.

Delete action: none.

## Audio Reachability

Push/Flip action audio is production-bound.

Evidence:

- `GameplayActionAudioRequestPlanner` maps action signals for `GameplayActionKind.Push` and `GameplayActionKind.Flip`.
- `Player_S1.prefab` has `GameplayActionAudioAuthoring` and references profile GUID `42a2e109fc5141ec9e866925a0a85c3b`.
- That GUID belongs to `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset`.
- Runtime/smoke tests assert Player_S1 action audio profile coverage.

Classification:

- Push/Flip audio binding: `KEEP_CURRENTLY_USED`.
- `_Test` profile name: `RENAMED_RETAINED`; the asset was renamed to `Player_S1_GameplayActionAudioProfile.asset` while preserving GUID `42a2e109fc5141ec9e866925a0a85c3b`.

Delete action: do not delete; rename/migration only. Profile content and Push/Flip action-audio entries remain unchanged.

## Tests-Only Areas

| Area | Current role | Classification | Delete stance |
| --- | --- | --- | --- |
| Direct `PlayerTickCommand.PushPressed/FlipPressed` construction | Scenario/unit coverage of command contract | KEEP_TEST_GOVERNANCE when testing command edges | Do not delete just because tests build commands directly |
| Plain move into Push box no-op/suppression | Guards against old auto-push fallback returning | KEEP_TEST_GOVERNANCE | Keep or rename tests to clarify removed fallback governance |
| Legacy ordinary fallback aliases | Diagnostic/replay compatibility | REMOVED_ALIAS | Canonical names retained; `EnableLegacyOrdinaryUnitFallback` field intentionally not deleted |
| Stage presentation legacy id validator tests | Former migration guard for serialized content | REMOVED_LEGACY_FIELD | Legacy `StageSpawnDefinition.PresentationId` read path and tests removed after asset migration |

## Runtime Delete Candidates

| Priority | Candidate | Classification | Required action |
| --- | --- | --- | --- |
| P1 | `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` | REMOVED_ALIAS | Tests/docs migrated to canonical removed-diagnostic preset. |
| P1 | `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackEnabled` | REMOVED_ALIAS | Tests/docs migrated to canonical removed-diagnostic helper. |
| P1/P3 | `GameplayRuntimeFeatureFlags.EnableLegacyOrdinaryUnitFallback` | DELETE_CANDIDATE_LEGACY_INPUT_COMPAT | Requires API/replay compatibility decision. |
| P1 | `StageSpawnDefinition.PresentationId` | REMOVED_LEGACY_FIELD | Migrated to `StagePresentationDefinition`, content and validators updated. |
| P1/P3 | `GroupId` / `SourceActionGroupId` compatibility aliases | REMOVED_ALIAS | Tests migrated to `ActionPlanId` / `SourceActionPlanId`; `IntentId` remains canonical internal ID. |
| P2/P3 | Push/Item stale trace ledger rows | REMOVED_STALE_LEDGER | Active stale legacy Push/Item trace-token rows removed from DeferredStale and Lane-A ledgers. |
| P2/P3 | `Player_S1_GameplayActionAudioProfile.asset` name | RENAMED_RETAINED | Renamed asset/profile; GUID preserved. |

Not found as active runtime artifacts:

- `PushBox`, `FlipBox`, `MovableBox`, `InteractableBox`, `PlayerPushController`, `PlayerFlipController`.
- `MovePush`, `AutoPush`, `PlainMovePush`.
- Push contact accumulation / threshold runtime fields.
- Old generated input wrapper.
- Dead Push/Flip `GameplayInputHost` route.
