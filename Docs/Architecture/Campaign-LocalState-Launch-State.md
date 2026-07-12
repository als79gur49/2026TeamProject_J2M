# Campaign LocalState Launch State

Campaign progression truth remains `Saves/profile.json`. The profile schema owns campaign slots, stage clear profile records, legacy import markers, deleted-slot guards, and `LastPlayedSlotNumber` profile metadata.

`Saves/local-launch-state.json` is a separate non-Cloud local launch state document. Its current schema is version 1 and stores only the campaign active launch pointer:

```json
{
  "schemaVersion": 1,
  "campaign": {
    "activeSlotNumber": 1,
    "lastUpdatedUtc": "2026-07-12T00:00:00Z"
  }
}
```

`activeSlotNumber` may be absent or `0`, which means no active local launch slot. A loaded active slot is valid only when it points at an existing non-empty campaign profile slot. If the LocalState file is valid, it wins over PlayerPrefs. If the LocalState file is missing, `Game.Feature.Stages.ActiveStageClearSaveSlot` may be imported once after validating the pointed slot against profile slots. The PlayerPrefs key is preserved and is not deleted by the import bridge.

Corrupt or schema-invalid `local-launch-state.json` does not silently fall back to PlayerPrefs. Callers must treat it as no active slot or a repair-required local state so the wrong campaign slot is not launched.

Pending launch state remains scene-transition/session-only and is not serialized as a `pendingLaunchSlotNumber` field in LocalState or profile. `CampaignRunningSlotContext` remains gameplay runtime-only and is not serialized in LocalState or profile.

DirectPlay temp state remains outside production LocalState. `Game.Feature.Stages.DirectPlay.TempSaveSlots` and `Game.Feature.Stages.DirectPlay.TempActiveSaveSlot` stay in their editor-temp namespace until a separate editor-temp policy slice.

Cloud boundary:

- `profile.json` is the campaign progression truth and the future Cloud include candidate.
- `local-launch-state.json` is local launch UX state and excluded from Cloud.
- PlayerPrefs `ActiveStageClearSaveSlot` is retained as an import source and cleanup is deferred.
- StageClearSaveSlots PlayerPrefs cleanup/delete/read-disable remains a separate future evidence-gated slice.
