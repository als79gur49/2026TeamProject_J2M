# Campaign LocalState Launch State

Campaign progression truth remains `Saves/profile.json`. The profile schema owns campaign slots, stage clear profile records, legacy import markers, deleted-slot guards, and metadata such as `LastPlayedSlotNumber`. `LastPlayedSlotNumber` is not a launch identity, default selection, Quick Continue source, or gameplay mutation target.

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

`activeSlotNumber` may be absent or `0`, meaning no committed local active slot. The active commit point is inside the gameplay installer, after confirming that the profile slot is non-empty and that request/handoff, `StageLaunchContextStore`, resolved stage content, and profile `CurrentStageId` identify the same stage. Commit order is `SetActiveSlot`, create `CampaignRunningSlotContext`, then consume the matching pending token.

NewGame and Continue do not update LocalState. Pre-commit validation, cinematic, transition, or load failure leaves the previous committed active unchanged. If commit completion fails after `SetActiveSlot`, the installer restores the previous active value before surfacing the failure. Clearing a pending request never clears committed active.

A loaded active slot is usable only when it points to an existing non-empty campaign profile slot. A valid LocalState file wins over PlayerPrefs. When LocalState is missing, `Game.Feature.Stages.ActiveStageClearSaveSlot` may still be imported after profile-slot validation; the retained PlayerPrefs key is not deleted. Corrupt or schema-invalid LocalState never silently falls back to PlayerPrefs.

## Pending application-session handoff

`CampaignLaunchHandoffSessionStore` is the non-MonoBehaviour application-session owner. Its immutable `CampaignLaunchHandoff` binds slot, `StageId`, navigation kind, source, and a unique token.

- The first accepted request wins; a second request cannot overwrite it.
- Clear and consume require the matching token.
- The request survives MainMenu cinematic playback and the required scene transition.
- Cinematic failure/cancellation, transition rejection, load failure, and installer pre-commit validation failure clear only the matching request.
- `RuntimeInitializeOnLoadType.SubsystemRegistration` resets the owner, so process/application-session restart never restores pending state.
- Pending state is not written to profile, LocalState, PlayerPrefs, Steam Cloud, or another file.

## Running scene-local slot

`CampaignRunningSlotContext` is created by the gameplay installer after active commit and remains scene-local. Gameplay chance reads, deaths, retries, stage clear, and other campaign mutations stay pinned to this context even if persistent active later changes. Retry and next-stage reload may recreate it from a previously committed active slot without a new MainMenu handoff.

DeleteSlot repairs matching active and pending independently. ClearAll clears both. Neither DTO gains pending or running fields.

## DirectPlay exception

DirectPlay temp keys and context remain in their editor-only namespace:

- `Game.Feature.Stages.DirectPlay.TempSaveSlots`
- `Game.Feature.Stages.DirectPlay.TempActiveSaveSlot`

Campaign temp DirectPlay continues to use the temp save/active context. Production Campaign DirectPlay remains the explicit overwrite/prime exception that may write committed active before gameplay startup. DirectPlay does not create or consume the normal production handoff.

## Cloud boundary

- `profile.json` is campaign progression truth and the future Cloud include candidate.
- `local-launch-state.json` contains committed local active only and remains excluded from Cloud.
- Pending and running state have no persisted representation.
- PlayerPrefs active and campaign save keys remain retained under their existing import/rollback policies.
