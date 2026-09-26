# Pre-Release Save Baseline Policy

Status: current canonical policy

VectorQuake has not been publicly released. There is no public prior product
version, public prior save schema, or public prior Store artifact that requires
compatibility. The current project version remains release-planning metadata
and does not create a prior-save compatibility obligation.

## First Public Save Contract

- The first public canonical campaign progression document is
  `Saves/profile.json`, using `CampaignProfileDocument.SchemaVersion = 3`.
- The first public canonical local session pointer is
  `Saves/local-launch-state.json`, using local-state schema version 1.
- PlayerPrefs campaign progression import is unsupported.
- A product-version change is not a save-schema migration trigger.
- `ProductVersion` is informational metadata, not a compatibility gate.
- Atomic temp-to-replace writes use file-specific `.write.<guid>.tmp` files and
  a durable `<file>.rollback` fallback for platforms without replace support.
  A later read/write recovers that rollback before deleting only the matching
  write temps. Profile `.bak` recovery, corrupt-file quarantine, current
  profile validation, and current slot validation are also current safety
  contracts. Local launch state intentionally has no automatic `.bak` fallback.
- Persisted current-schema slot data is validated before normalization. Negative
  persisted slot counters and invalid or duplicate nested performance and
  stage-clear records fail closed instead of being silently clamped, dropped,
  or rewritten by an unrelated save. Schema 3 stores fixed `GameMode` and `ResumeHp` per occupied slot.
  Casual requires HP `1..3` and reserved inactive Chance `0`; Hardcore requires
  reserved HP `0` and Chance `1..3`. Unknown modes are invalid. Schema 1/2 profiles
  return UnsupportedVersion without restoring backup, conversion, or initialization.
  Casual death commits the current level-group first stage and HP 3; Hardcore's
  last Chance commits the sequence first stage and Chance 3. Both preserve history
  and increment TotalDeaths once per successful death commit. Clear restores Casual
  HP 3; manual restart, menu, and relaunch retain saved HP. Hardcore retains authored
  HP/timing and its existing clear Chance rules. DirectPlay validates survival before writes.
  `SaveSlotData.Clone()` and nested clones are exact deep copies retained only at
  the explicit raw DTO/diagnostic/test boundary: they preserve nulls, invalid
  values, null elements, duplicates, and order without validating or normalizing.
  `CampaignSlotRawDataMapper` is a non-mutating validating raw conversion boundary,
  not an exact-clone owner or runtime business persistence seam. It may materialize
  an allowed nullable container representation, but it must reject rather than
  repair an invalid receipt string pair or other invalid raw shape. Exact raw copy
  belongs to the clone methods and `CampaignSlotRawDocumentCloner`.
  The parser classifies physical absence as an empty `CampaignSlotEntry`, retains
  mismatched or invalid raw documents in `CampaignSlotDiagnostic`, and constructs
  immutable `CampaignSlotState` only from valid documents. Empty-shaped data with
  an invalid slot/chance/death envelope is diagnostic/profile failure, not Empty.
  The repository-owned post-validation document materializer materializes allowed
  nulls and synchronizes receipt presence only after validation. Receipt serializer
  residue has exactly two valid in-memory string shapes: both receipt strings are null
  (reading an empty nested JSON object), or both are empty
  (round-tripping a null nested object through the Unity writer). Version and source
  must both be zero. Mixed null/empty receipt strings, whitespace, and non-default
  values are invalid before conversion or materialization. This is a post-`JsonUtility`
  object-shape contract, not a raw JSON-token provenance contract: an individual JSON
  string `null` becomes empty after deserialization, so its original token provenance
  is unavailable. Mixed pairs are rejected at direct in-memory raw/document inputs. For
  `HasNormalCampaignCompletionReceipt == false`, any non-default payload fails closed
  before materialization and cannot be dropped by an unrelated write. With the flag
  true, either exact serializer residue restores `PresentWithoutPayload`.
  `CampaignSlotState` construction owns performance uniqueness and deterministic
  ordering, while `CampaignSlotTransitionEngine.UpsertPerformance` owns best-value
  updates. `CampaignSlotStateDocumentMapper`
  performs a strict, order-preserving one-to-one projection of canonical state; it
  does not validate, skip invalid elements, clamp counters, or invoke tolerant
  business normalization. Achievement consumers receive immutable canonical
  slot/performance state directly; malformed persisted records fail at the
  profile/parser boundary and are never projected or repaired by achievement code.
- Death, clear, and survival HP commits use ordinary Save. A small root-shared
  synchronous gate covers load/validate/mutate/persist and rejects nested mutations.
  Notifications run after the operation. Death wins over clear, which wins over HP.
  Save failure or an unknown outcome abandons that scene's run, including catch-up
  ticks and forced clear. The error popup offers Main Menu / Quit. Existing menu
  Continue reads the persisted file for a fresh run, while a completed slot shows
  its existing Completed card. This flow never reapplies the failed command. The latest unsaved result can be lost. No persistent
  operation receipt, slot incarnation ID, or retry protocol is introduced.
- Standalone QA seed JSON uses version 2 with explicit `GameMode` and `ResumeHp`;
  old seed files are rejected, and capture/DirectPlay retain their isolated roots.
- Ordinary Main Menu Continue never submits a complete replacement slot. It reserves
  a launch handoff, then sends the expected slot/stage/persisted-level-group and
  resolved target level-group through `ICampaignContinuePreparationPort`. The save
  service re-reads and verifies that identity in the same mutation operation, changes
  only `CurrentLevelGroupId` when synchronization is required, and returns the
  committed immutable state. An already-current group verifies without a write;
  missing, completed, or stale identity fails without a write and cannot route.
  General complete-slot replacement and its concrete/internal fixture seam are
  removed. DirectPlay/player-capture seed setup uses the narrow
  `ICampaignSlotSeedImportPort`; diagnostics and lifecycle operations likewise use
  purpose-specific commands rather than accepting an externally assembled slot.
- Main Menu slot presentation consumes an immutable `CampaignSlotEntry`, its
  repository-free `CampaignSlotLaunchEvaluation`, and the derived
  `CampaignSlotActionPolicy` as separate inputs. The retired combined validation
  result/service and its corrected mutable clone are not runtime surfaces. Profile
  parse/schema/IO failures remain a global `CampaignSaveLoadReport` blocked screen;
  sequence/catalog launch failures remain occupied per-slot cards with restart/delete
  actions. Presentation does not reinterpret malformed raw evidence as an empty slot.
- DeleteSlot, ClearAll, and NewGame/Restart initialization are destructive
  profile commits: their post-mutation document is also the recovery snapshot,
  so automatic backup recovery cannot resurrect the prior slot state.
  Canonical and backup interrupted-write rollbacks are normalized before the
  previous two-file state is captured.
  If the canonical half of that two-file commit fails, the repository attempts
  to restore both prior files independently. An incomplete compensation is
  surfaced as an aggregate failure, and a newly written recovery backup is not
  discarded when restoring the canonical file also failed.
- Unknown save schemas fail closed without restoring an older-schema backup or
  mutating the canonical file. They do not fall back to PlayerPrefs.
- A blocked-profile reset normalizes and archives the backup before the
  canonical profile. Its pending marker remains until both active sources are
  archived and the empty reset profile is durably written, so retry cannot
  promote the previous backup back to canonical state.
- A missing `profile.json` is the normal no-save/new-user state only when no
  valid current-schema `profile.json.bak` exists. A valid backup is restored
  before reporting `Missing`.
- A missing `local-launch-state.json` means there is no committed active slot.
- NewGame/empty Continue and confirmed Restart/Overwrite durably initialize the
  profile before routing. Route rejection does not roll that profile write back.

## Unsupported Pre-Release Data Policy

- Internal QA campaign progression is not a public compatibility target.
- Pre-release saves may be reset after source changes.
- The retired pre-release `stage-5-1` cursor is not auto-repaired or migrated to
  the current final stage. It follows the ordinary sequence-missing launch
  failure path without rewriting the slot. Catalog-only `legacy-stage-5-1`
  content and alias/catalog governance remain independent of save compatibility.
- Audio, display, input, and locale PlayerPrefs settings are outside campaign
  progression reset scope and must be preserved.
- Broad registry namespace deletion is forbidden.
- Unknown PlayerPrefs keys must not be deleted.
- Editor DirectPlay campaign data uses an isolated disposable JSON root; there are no campaign DirectPlay PlayerPrefs keys.
- Any operational QA reset requires separate approval and exact-key/file scope.

Legacy deleted-slot resurrection guards existed only because a legacy importer
could recreate deleted slots. They are not part of the first-public save
contract. Current deletion repair remains required: deleting a current profile slot must still clear matching committed active state and matching pending
launch ownership. ClearAll and blocked-profile reset must repair the same
current launch state without PlayerPrefs markers.

## Artifact Retention Boundary

- Historical Store artifacts are immutable private evidence.
- Historical Store artifacts are not current release-pipeline input.
- Historical Store artifacts must not be promoted as future release candidates.
- Existing provenance evidence must not be edited or repackaged by this change.
- Git history remains the compatibility-design provenance.

## Canonical Persistence Boundary

| Concern | Canonical persistence | PlayerPrefs status |
| --- | --- | --- |
| Campaign progression | `Saves/profile.json` | unsupported |
| Committed local active slot | `Saves/local-launch-state.json` | unsupported |
| Pending launch | application-session memory | never persisted |
| Running slot context | scene-local memory | never persisted |
| Audio/display/input/locale settings | existing settings stores | supported |
| Editor DirectPlay temporary state | isolated `Library/J2M/DirectPlayCampaign/Saves` JSON root | unsupported |
| Player capture temporary state | process-scoped `temporaryCachePath/J2M/PlayerCaptureCampaign/<process-guid>/Saves` JSON root | unsupported |

Production campaign composition must not construct a PlayerPrefs progression
store, legacy importer, migration coordinator, rollback composition, legacy
marker store, or active-slot PlayerPrefs import bridge. Existing campaign
PlayerPrefs values are ignored and left unchanged; production does not delete
them.

The file contract assumes one in-process writer for a save root. Cross-process
writer coordination and a new transaction/commit-marker schema are not part of
the first-public contract.
