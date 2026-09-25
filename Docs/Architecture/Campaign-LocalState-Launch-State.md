# Campaign LocalState Launch State

Campaign progression truth remains `Saves/profile.json`. The profile schema owns campaign slots, stage clear profile records, performance records, comic completion, and metadata such as `LastPlayedSlotNumber`. `LastPlayedSlotNumber` is not a launch identity, default selection, Quick Continue source, or gameplay mutation target.

## Committed local active

`Saves/local-launch-state.json` is a separate non-Cloud local document. Schema version 1 stores only the most recent campaign slot whose gameplay installer validated the profile slot and launch stage identities:

```json
{
  "schemaVersion": 1,
  "campaign": {
    "activeSlotNumber": 1,
    "lastUpdatedUtc": "2026-07-12T00:00:00Z"
  }
}
```

`activeSlotNumber` may be absent or `0`, meaning no committed local active slot. The active commit point is inside the gameplay installer, after confirming that the profile slot is non-empty and that request/handoff, `StageLaunchContextStore`, resolved stage content, and profile `CurrentStageId` identify the same stage. Commit order is `SetActiveSlot`, create `CampaignRunningSlotContext`, consume the exact pending handoff, then consume/finalize the exact `StageLaunchContext`. These four steps are one explicit installer transaction. Once `SetActiveSlot` has been attempted, any later failure assumes the storage may already have mutated and restores the previous active (or empty active when none existed), discards the uncommitted running context, and clears only the still-matching pending/context operation. Compensation steps continue independently; rollback or cleanup failures are surfaced together with the original failure as an aggregate diagnostic rather than hidden.

NewGame, Restart, and Continue do not update LocalState. Pre-commit validation, intro comic presentation, transition, or load failure leaves the previous committed active unchanged. If commit completion fails after `SetActiveSlot`, the installer restores the previous active value before surfacing the failure. Clearing a pending request never clears committed active.

A loaded active slot is usable only when it points to an existing non-empty campaign profile slot. A missing canonical profile is reported as missing only when no valid current-schema profile backup exists; backup-only interruption states restore the valid backup before LocalState availability is classified. Profile availability is classified internally as available, definitely absent, or indeterminate. Loaded/backup-recovered non-empty slots are available; a missing profile or loaded empty slot is definitely absent and may clear a stale LocalState active value. Corrupt, unsupported-schema, I/O, authorization, and recovery-pending results are indeterminate: the read fails closed while preserving the committed LocalState file for a later retry. LocalState is the only production persistence source for committed active state. Missing LocalState means no committed active slot. Corrupt or schema-invalid LocalState has no automatic `.bak` fallback and never falls back to PlayerPrefs. Existing campaign active-slot PlayerPrefs values are ignored and left unchanged.

## Pending application-session handoff

`CampaignLaunchHandoffSessionStore` is the non-MonoBehaviour application-session owner. Its immutable `CampaignLaunchHandoff` binds slot, `StageId`, navigation kind, source, and a unique token.

- The first accepted request wins; a second request cannot overwrite it.
- Clear and consume require the matching token.
- MainMenu NewGame, Restart, and empty-slot Continue create the complete handoff before profile initialization or validation sync can write. The handoff acts as the application-session launch reservation; acquiring it does not commit persistent active.
- Overwrite and restart confirmations capture the reservation token, slot, and operation kind. A callback may initialize the profile at most once and only while that exact operation remains current. Cancelled, duplicate, replaced, or otherwise stale callbacks are no-ops and cannot clear a newer operation.
- Once NewGame, empty-slot Continue, or a confirmed Restart/Overwrite owns the reservation, MainMenu durably initializes the canonical profile slot before routing. A later comic/scene transition rejection clears only the transient launch ownership; it does not roll the initialized profile back to the previous slot document.
- MainMenu confirmation callbacks are controller-lifetime bound. Disposal invalidates launch, delete, and blocked-profile-reset callbacks, releases any confirmation-owned handoff, and prevents late ViewModel publication.
- The request survives MainMenu intro comic presentation and the required scene transition.
- Every intro comic terminal callback revalidates the captured handoff against the current pending owner by token, slot, `StageId`, navigation kind, and source. A missing or non-exact current owner makes Completed, Failed, and Cancelled a complete no-op: no profile/progress write, gameplay route, `StageLaunchContextStore` write, active write, or pending clear.
- An intro comic presentation claims at most one valid terminal callback at the router transaction boundary. Completed followed by Cancelled, Failed followed by Completed, and duplicate terminal notifications cannot repeat routing, progress, or cleanup even if the view/coordinator also has a completion guard.
- Valid Completed first requests gameplay routing. `IntroComicCompleted` is committed only after the route accepts synchronously; it means that the intro comic terminal result remained owned and gameplay routing was accepted. Immediate transition rejection or a route exception writes no intro progress and clears only the still-matching handoff.
- Valid Failed and Cancelled never write progress or route gameplay and clear only the still-matching owner. DeleteSlot and ClearAll repair matching pending ownership as part of the operation, so a callback arriving after either operation returns cannot recreate a deleted slot or launch context.
- Transition rejection, load failure, and installer pre-commit validation failure clear only the matching request.
- `StageLaunchContext` is an immutable in-memory operation identity carrying token, slot number, `StageId`, navigation kind, and source. Registration is strict first-owner-wins: once current exists, a second registration is rejected even when all five identity fields are equal. Production set, clear, and consume use the full identity rather than stage-only or token-only approval.
- Scene-load completion/failure handling captures that full context. A callback claims at most one terminal result and only while the captured context and, for a normal launch, pending handoff are still exact current owners. Late and duplicate callbacks cannot clear a newer same-stage operation.
- Configured and current-scene routers share the same immediate-acceptance contract. A transition guard rejection is surfaced to the caller and does not start a load or delete temporary campaign JSON. When the configured router consumed a DirectPlay bootstrap before the rejected attempt, it restores that exact bootstrap and the prior DirectPlay context only while the captured DirectPlay ownership generation is unchanged and no newer pending prime exists.
- A coordinator failure after synchronous acceptance follows the same ownership rule: it restores the carried DirectPlay context only while the ownership generation captured for that transition is still current. A newer set or clear operation makes the late failure a no-op, even when the store is empty again.
- Gameplay installer resolution/profile validation, active snapshot/write, running pin, pending consume, and context consume share one failure boundary. Success leaves neither pending nor launch context behind.
- `RuntimeInitializeOnLoadType.SubsystemRegistration` resets the owner, so process/application-session restart never restores pending state.
- Pending, `StageLaunchContext`, and running state are not written to profile, LocalState, PlayerPrefs, Steam Cloud, or another file.

Intro comic callbacks never write `Saves/local-launch-state.json`. Persistent active remains committed only by the gameplay installer after its profile, request, handoff, context, and resolved-stage validation. A route accepted synchronously but failing later during asynchronous scene loading remains transition-owner follow-up work; the intro comic boundary does not claim that later outcome.

## Running scene-local slot

`CampaignRunningSlotContext` is created by the gameplay installer after active commit and remains scene-local. Gameplay chance reads, deaths, retries, stage clear, and other campaign mutations stay pinned to this context even if persistent active later changes. Retry and next-stage reload may recreate it from a previously committed active slot without a new MainMenu handoff only when an exact `StageLaunchContext` exists and its navigation/source is on the committed-reload allowlist. The allowlist is `stage-result-retry`, `pause-retry`, `campaign-death-retry`, and `level-failed-restart-level` for Retry, plus `campaign-auto-next` for NextStage. Continue, arbitrary startup, and unrelated direct scene entry cannot use active fallback.

Campaign stage clear commits the next stage and its canonical level group to the running profile slot. In Hardcore, a boundary between canonical level groups restores `RemainingChances` to `CampaignSaveSlotPolicy.DefaultRemainingChances`; advances within one group preserve the current value, and final campaign clear has no next-world Chance restoration. The completed scene retains its pre-restoration Chance display with change audio suppressed, while the next gameplay scene reads the saved value on its initial HUD bind. In Casual, stage clear keeps the inactive `RemainingChances` value at `0` and restores saved `ResumeHp` to `CampaignSaveSlotPolicy.CasualMaxHp` (`3`).

`DeleteSlot` repairs matching active and pending independently. `ClearAll` clears both. MainMenu Continue uses `ICampaignContinuePreparationPort`, whose command carries expected slot/stage/persisted-group and target-group identity only; it cannot smuggle deletion or a complete slot replacement through preparation. Missing, completed, or stale preparation is a no-write result, and MainMenu clears only its original handoff token before refreshing. Launch-state repair therefore remains lifecycle-owned. MainMenu does not open a delete confirmation while any accepted launch handoff is pending; store-level repair remains the authority for non-UI deletion. Neither DTO gains pending or running fields.

## DirectPlay exception

Campaign temp DirectPlay uses the canonical schema-3 profile and schema-1 local-state services under an isolated disposable save root. In the Editor the root is `<project>/Library/J2M/DirectPlayCampaign/Saves`. Player diagnostics use a process-lifetime GUID root at `Application.temporaryCachePath/J2M/PlayerCaptureCampaign/<process-guid>/Saves`, so all providers in one process share a root while a later process cannot inherit it accidentally. Normal Player quit cleanup validates the governed parent and exact GUID leaf before deleting only that process scope; forced-termination residue is not pruned by another live process. The context carries no persistence keys and selects this composition only through `CampaignTempSlot` mode.

Production Campaign DirectPlay remains the explicit overwrite/prime exception that may write committed active before gameplay startup. DirectPlay does not create or consume the normal production handoff. Normal pending and DirectPlay identities are not merged, and `editor-direct-play` is not a pending-less committed-reload source. Explicit temp cleanup deletes only the isolated profile, local state, pending reset, backups, and temp files.

Subsystem registration resets only the runtime `StageLaunchContext`. The editor `SessionState` DirectPlay stage prime survives PlayMode entry and is removed only when gameplay consumes it or an explicit editor/launcher cleanup calls the full clear path. DirectPlay context/session delegates remain behind the editor boundary; runtime assemblies do not reference `UnityEditor`.

## Recovery limits

- Deterministic scene-load completion/failure callbacks, transition setup exceptions, and coordinator `OnDestroy` cleanup are covered by exact captured operation identity.
- Application-session reset clears runtime launch ownership but deliberately preserves a pre-PlayMode Editor DirectPlay prime until consume or explicit editor cleanup.
- A watchdog for a live `AsyncOperation` that never invokes a terminal callback is deferred; no arbitrary timeout is introduced here.
- Failure to restore persistent active is fatal for the launch attempt and is surfaced in the aggregate diagnostic. The installer transaction has no separate recovery schema or transaction journal; the file writer's implementation-level `.rollback` artifact is not gameplay state or a new save schema.
- Async `IntroComicCompleted` rollback, a unified DirectPlay/pending protocol, and manual Player E2E remain separate follow-up work.

## Cloud boundary

- `profile.json` is campaign progression truth and the future Cloud include candidate.
- `local-launch-state.json` contains committed local active only and remains excluded from Cloud.
- Pending, `StageLaunchContext`, and running state have no persisted representation.
- PlayerPrefs active and campaign progression compatibility is unsupported. Existing values are ignored rather than deleted.
