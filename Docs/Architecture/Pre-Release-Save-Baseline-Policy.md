# Pre-Release Save Baseline Policy

Status: current canonical policy

VectorQuake has not been publicly released. There is no public prior product
version, public prior save schema, or public prior Store artifact that requires
compatibility. The current project version remains release-planning metadata
and does not create a prior-save compatibility obligation.

## First Public Save Contract

- The first public canonical campaign progression document is
  `Saves/profile.json`, using `CampaignProfileDocument.SchemaVersion = 2`.
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
  or rewritten by an unrelated save. Zero remaining chances retains its current
  sentinel meaning and is not rejected by this structural validation.
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
