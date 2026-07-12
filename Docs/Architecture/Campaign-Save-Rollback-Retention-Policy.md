# Campaign Save Rollback Retention Policy

This policy locks the rollback and retention meaning after the campaign
profile-backed production switch. It is documentation and guard coverage only.
It does not change production save behavior, delete PlayerPrefs keys, remove
legacy markers, migrate local state/settings JSON, or add Steam APIs, Steam
Cloud, or SteamPipe VDF configuration.

## Production Truth

- Production campaign progression truth is `Saves/profile.json` through the
  profile-backed campaign save path.
- `Game.Feature.Stages.StageClearSaveSlots` PlayerPrefs is no longer a
  production campaign payload output.
- `Game.Feature.Stages.StageClearSaveSlots` PlayerPrefs is retained only as a
  legacy import source and explicit rollback source.
- Production campaign paths must not write, delete, or save the retained
  `StageClearSaveSlots` PlayerPrefs payload.

## Rollback Provider Semantics

`CampaignSaveCompositionProvider.CreateProductionLegacyRollback` semantics:

- Uses the retained `StageClearSaveSlots` PlayerPrefs legacy view.
- Does not read `profile.json` as production truth.
- Does not backfill profile-era progress into PlayerPrefs.
- Does not delete `profile.json`.
- Does not modify `profile.json`.
- Is not user-facing continuity.
- Is an operator/dev fallback.

Rollback provider may show stale legacy data. This is expected. It must not be presented as a user-facing save continuity path.

## Retention Window

StageClearSaveSlots PlayerPrefs retention policy:

- Retain `Game.Feature.Stages.StageClearSaveSlots` for at least 2 profile-backed public releases.
- During this window, keep the payload as the import source for legacy-only
  saves.
- During this window, keep the payload as the retained legacy view for the
  explicit rollback provider.
- Cleanup/delete may be considered only in a separate future investigation
  after the retention window and evidence gate are both satisfied.

## Cleanup Gate

Cleanup/delete preconditions:

1. Profile-backed production switch is stable in public release.
2. Legacy-only PlayerPrefs save to `profile.json` import smoke passed.
3. Valid profile plus stale PlayerPrefs ignored smoke passed.
4. Profile-backed NewGame, stage clear, delete, clear all, and relaunch smoke
   passed.
5. Corrupt profile blocks fallback smoke passed.
6. DeleteSlot no resurrection evidence captured.
7. ClearAll no remigration evidence captured.
8. Rollback provider is documented as an operator/dev fallback.
9. Marker migration/removal policy is decided separately.
10. Steam Cloud include/exclude policy is reviewed again before cleanup.

No cleanup/delete slice may use this policy alone as permission to delete the
retained PlayerPrefs payload. The future investigation must restate the current
release count, evidence status, rollback impact, and Steam Cloud inventory
decision.

## Marker Relationship

`CampaignProfile.Legacy*` marker writes remain in place:

- `Game.Feature.Stages.CampaignProfile.LegacyImportDisabled`
- `Game.Feature.Stages.CampaignProfile.LegacyImportedSourceHash`
- `Game.Feature.Stages.CampaignProfile.LegacyResetTombstoneUtc`
- `Game.Feature.Stages.CampaignProfile.LegacyDeletedSlotGuards`

Marker removal is not ready in this slice. Marker removal must be considered
only after the `StageClearSaveSlots` retention policy window and in a separate
future slice. Immediate marker deletion can cause ClearAll remigration or
DeleteSlot resurrection by losing the local evidence that blocks legacy import.

Marker migration/removal is a separate future slice and must include explicit
ClearAll no-remigration and DeleteSlot no-resurrection evidence.
