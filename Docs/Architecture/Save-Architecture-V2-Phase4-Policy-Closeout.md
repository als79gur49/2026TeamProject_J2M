# Save Architecture V2 Phase 4 Policy Closeout

This note closes the Phase 1-4 policy audit gaps before Phase 5 investigation.
It is a policy handoff only. It does not switch production storage, enable
profile.json writes in production, migrate production call sites, or add Steam
APIs.

## Verdict

- Overall closeout: policy-only.
- Phase 5 readiness: ready with notes.
- Production behavior changed: no.
- Production storage switched: no.
- Steam API added: no.

## Phase 4 State

- `CampaignSaveService`, `SaveSlotStoreCompatibilityAdapter`,
  `CampaignSaveMigrationCoordinator`, and `LegacyPlayerPrefsCampaignImporter`
  are retained outside production composition.
- The production `SaveSlotStore()` public constructor still uses the canonical
  PlayerPrefs backend and `SaveSlotPrefsKeys.SaveSlotsKey`.
- `CampaignSaveMigrationOptions.EnableProfileWrite` remains false by default,
  so legacy import can be inspected without enabling production profile writes.
- `CampaignSaveService.ClearAll` writes profile-level reset/import-disabled
  metadata and may mark local legacy import disabled through
  `ICampaignSaveResetMarkerPort`, but it does not delete the retained legacy
  PlayerPrefs campaign payload.

## DeleteSlot Legacy Guard Policy

`DeleteSlot` removes the slot from the V2 profile document, adjusts
`LastPlayedSlotNumber`, and records a deleted-slot legacy guard so retained
legacy PlayerPrefs data cannot automatically resurrect the deleted slot.

The guard document is `CampaignLegacyDeletedSlotGuardDocument`:

- `SlotNumber`
- `ImportedSourceHash`
- `DeletedAtUtc`
- `Reason = "DeleteSlot"`

Guard behavior:

- Same imported source hash: guarded slots are filtered from automatic legacy
  import, while unguarded legacy slots from the same source may still import.
- Changed imported source hash: if the changed legacy source contains a guarded
  deleted slot, automatic import returns `MigrationDeferred` and performs no
  profile write. This avoids overwriting the user's deletion decision with an
  ambiguous changed legacy source.
- Empty `ImportedSourceHash`: treated as a source-agnostic guard for that slot.
  It blocks automatic resurrection of that slot from any legacy source hash.
- `LastPlayedSlotNumber`: if it points at a guarded/deleted slot after filtering,
  it is recomputed to an imported unguarded slot and must not point back to the
  guarded slot.
- `ClearAll`: full reset/import-disabled/reset tombstone policy supersedes
  slot-level guards. ClearAll writes an empty valid profile with
  `ImportDisabled = true` and `ResetTombstoneUtc`, clears retained slot guards in
  the profile document, and blocks later legacy import through the local reset
  marker when the marker port is present.
- `InitializeNewGame`: creating a new V2 slot after deleting an imported legacy
  slot must not remove the retained delete guard for the older legacy source.
- Old/null profile compatibility: profiles that omit `DeletedSlotGuards` or
  deserialize it as null normalize safely to an empty guard array.

This guard policy does not delete retained legacy PlayerPrefs payloads and does
not switch production storage. Production integration remains deferred until a
separate production switch decision validates SaveSlotStore call-site migration,
profile write enablement, and adapter wiring on the same revision.

## Active Slot Split Defer

`LastPlayedSlotNumber` and pending launch slot are separate concepts.

`CampaignProfileDocument.LastPlayedSlotNumber` is profile metadata and can be
Cloud-friendly because it records the last played campaign slot in the profile
document.

The existing `ActiveSlotProvider` behavior represents the pending launch slot,
which is local/session state. Phase 5 must not treat pending launch slot as
campaign progression and must not move it into Cloud-targeted profile metadata
as if it were equivalent to `LastPlayedSlotNumber`.

Existing `ActiveSlotProvider` behavior remains production-local until an explicit active slot split phase.
A later split may decide how to represent
launch intent, menu selection, and profile metadata separately, but that is not
part of Phase 4 and must not be implied by Phase 5 adapter integration.

## Phase 5 Handoff

Phase 5 may investigate SaveSlotStore call-site migration and adapter production
integration only under these constraints:

- Do not change `SaveSlotStore()` default backend as part of this closeout.
- Do not enable production `profile.json` writes without an explicit switch
  decision and validation plan.
- Do not delete legacy PlayerPrefs campaign keys as part of this closeout.
- Do not treat deleted-slot guard coverage as permission to wire production
  `CampaignSaveService` or `SaveSlotStoreCompatibilityAdapter`.
- Do not implement the active slot split as part of this closeout.
- Do not add Steamworks.NET, Steam Cloud, `ISteamRemoteStorage`, or any Steam
  Remote Storage API as part of this closeout.
- Phase 5 must still include a production switch decision point before automatic
  legacy import can run in production against retained PlayerPrefs data.
