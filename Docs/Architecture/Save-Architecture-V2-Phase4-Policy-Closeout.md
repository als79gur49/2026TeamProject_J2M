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

## DeleteSlot Resurrection Risk

`DeleteSlot` in Phase 4 removes the slot from the V2 profile document and
adjusts `LastPlayedSlotNumber`, but it does not write a slot-level legacy deletion marker or tombstone.

Because retained legacy PlayerPrefs may still contain the deleted slot,
production integration must not allow automatic legacy reimport to resurrect
deleted slots. This is especially relevant if a V2 profile is missing,
quarantined, reset, or otherwise routed back through legacy import while the
legacy PlayerPrefs campaign source is still present.

Before or during the production switch, Phase 5 or Phase 6 must explicitly
select and test one remigration prevention policy:

1. Slot-level deletion marker/tombstone.
2. Imported legacy source hash plus deleted slot guard.
3. Legacy fallback disabled after profile reset/delete.
4. Another tested remigration prevention policy.

Until that selection is made, slot-level marker/tombstone work is deferred and
`DeleteSlot` must be treated as profile-document deletion only. Phase 5 planning
must carry this risk forward rather than assuming that Phase 4 deletion is a
complete legacy-source deletion guard.

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
- Do not add slot-level tombstone implementation as part of this closeout.
- Do not implement the active slot split as part of this closeout.
- Do not add Steamworks.NET, Steam Cloud, `ISteamRemoteStorage`, or any Steam
  Remote Storage API as part of this closeout.
- Phase 5 must include a decision point for retained legacy deletion risk before
  production legacy import can run automatically against retained PlayerPrefs
  data.
