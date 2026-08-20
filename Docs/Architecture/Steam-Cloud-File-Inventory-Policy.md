# Steam Cloud File Inventory Policy

This policy freezes the Steam Cloud file inventory for Steam Release Phase B. It does not enable Steam Cloud, add Steamworks.NET, call Steam APIs, delete legacy PlayerPrefs keys, or define SteamPipe VDF/admin settings.

## Current production save truth

- Campaign progression save truth: `Saves/profile.json` through `CampaignSaveCompositionProvider.CreateProductionProfileBacked()`.
- Retained legacy import / rollback source key: `Game.Feature.Stages.StageClearSaveSlots`.
- Active launch pointer file: `Saves/local-launch-state.json`.
- Retained active launch import source key: `Game.Feature.Stages.ActiveStageClearSaveSlot`.
- Pending launch state is application-session memory only and is not written to any file or PlayerPrefs key, including the active launch pointer file, and is not a Steam Cloud target.
- Production Campaign DirectPlay explicit active overwrite/prime remains an editor-launch exception; DirectPlay temp state stays in its existing temp namespace and neither path persists the normal production handoff.
- Current profile file: `Saves/profile.json`.
- Current Steam Cloud action: Auto-Cloud application is deferred. Revisit after the separate Steam Cloud enable decision.

## Company/Product path guard

- Unity Company: `J2M`.
- Unity Product: `VectorQuake`.
- Expected Windows persistent data path family: `AppData/LocalLow/J2M/VectorQuake`.
- Expected future save folder: `AppData/LocalLow/J2M/VectorQuake/Saves`.
- The Company/Product path `J2M/VectorQuake` must be frozen before applying Steamworks Auto-Cloud rules.
- Any pre-release Company/Product rename requires a Steam Auto-Cloud rule review before release content setup.

## Future Steam Auto-Cloud draft

Future only. Do not apply this rule until a separate Steam Cloud enable decision.

```text
Root:
- WinAppDataLocalLow

Subdirectory:
- J2M/VectorQuake/Saves

Pattern:
- profile.json

Recursive:
- false

Include:
- profile.json

Apply timing:
- after a separate Steam Cloud enable decision
- after validation confirms the production profile path remains Saves/profile.json
```

Policy constraints:

- Do not use a `*.json` include pattern.
- Do not enable recursion for the current inventory.
- The future include is exactly `WinAppDataLocalLow/J2M/VectorQuake/Saves/profile.json`.
- Steam Cloud minimum release readiness for this phase is the Auto-Cloud rule draft and guard coverage only. Runtime Steam API integration is out of scope.

## Steam Cloud exclusions

Exclude these files, patterns, PlayerPrefs keys, and artifact roots from Steam Cloud:

- `Saves/profile.json.bak`
- `Saves/profile.*.tmp`
- `Saves/profile.json.corrupt.*`
- `Saves/profile.json.rejected.*`
- `Saves/profile.json.bak.rejected.*`
- `Saves/profile.reset.pending.json`
- `Saves/profile.reset.pending.json.bak`
- `campaign-save-seed.json`
- `Settings/local-settings.json`
- `Saves/local-launch-state.json`
- `Saves/editor-direct-play.json`
- `Saves/direct-play-temp.json`
- `settings.audio.*`
- `settings.display.*`
- `Game.Feature.Input.*`
- `Game.Feature.Stages.ActiveStageClearSaveSlot`
- `Game.Feature.Stages.DirectPlay.TempSaveSlots`
- `Game.Feature.Stages.DirectPlay.TempActiveSaveSlot`
- `TestLogs/`
- `TestLogs/SaveReadiness/`
- `Logs/`
- `TestResults/`
- `ProfilerCaptures/`
- `*.log`
- `*.tmp`

Cloud exclusion notes:

- `profile.json.bak` is excluded by default.
- User-confirmed reset archives and the local pending-reset marker are excluded by default.
- Backup clouding requires a separate tested backup-cloud policy before reconsideration.
- Direct-play temp save and temp active-slot keys are never Cloud targets.
- Readiness reports are CI artifacts only and are not Cloud targets.
- Audio, display, and input settings remain outside the campaign save Cloud inventory.

## PlayerPrefs inventory freeze

This section freezes the current PlayerPrefs key and prefix inventory for the
PlayerPrefs Inventory Freeze slice. It is documentation and guard coverage only.
It does not switch `SaveSlotStore`, enable production `profile.json` writes, wire
`CampaignSaveServiceFactory` or `CampaignSaveService` into production, delete
PlayerPrefs keys, enable Steam Cloud, add Steam APIs, or add SteamPipe upload
configuration.

JSON target classifications:

- `CampaignProfileJson`: future `Saves/profile.json` campaign profile target.
- `LocalSettingsJson`: future local settings JSON target; never Cloud profile.
- `LocalLaunchStateJson`: committed local active launch pointer target; never Cloud profile.
- `EditorOnlyJson`: editor-only JSON target; never Cloud profile or release content.
- `DeleteOnlyLegacy`: legacy key retained until an explicit cleanup slice; no import target.
- `ImportOnlyLegacy`: legacy-only import source classification reserved for future source keys that must not become production write targets.
- `Remove`: key/prefix should be removed in a later explicit cleanup decision.
- `UnknownNeedsDecision`: observed key/prefix needs a product/owner decision before migration.

Frozen inventory:

| Key or prefix | Owner | Read | Write | Delete | Production read/write | JSON target | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Game.Feature.Stages.StageClearSaveSlots` | `SaveSlotStore`, `CampaignLegacySourceReader` | yes | yes | existing reset paths only | yes | `CampaignProfileJson` | Campaign save payload; future `Saves/profile.json` target; retained after switch as rollback/import source for a bounded period. |
| `Game.Feature.Stages.ActiveStageClearSaveSlot` | `PlayerPrefsActiveSlotStorage` import bridge, legacy tests | import only | no production write | no production delete | import source only | `LocalLaunchStateJson` | Retained active launch pointer import source; production writes go to `Saves/local-launch-state.json` and key cleanup is deferred. |
| `Game.Feature.Stages.SaveSlots` | legacy stage clear cleanup | no production read | no production write | existing cleanup paths only | no | `DeleteOnlyLegacy` | Old pre-stage-clear key; do not import into profile in this slice. |
| `Game.Feature.Stages.ActiveSaveSlot` | legacy active slot cleanup | no production read | no production write | existing cleanup paths only | no | `DeleteOnlyLegacy` | Old pre-stage-clear active key; do not import into profile in this slice. |
| `Game.Feature.Stages.DirectPlay.TempSaveSlots` | `EditorDirectPlayContextStore`, editor launcher | yes | yes | editor temp clear only | no | `EditorOnlyJson` | Direct-play temp state; later cleanup may choose `Remove`, but this slice keeps it editor-only. |
| `Game.Feature.Stages.DirectPlay.TempActiveSaveSlot` | `EditorDirectPlayContextStore`, editor launcher | yes | yes | editor temp clear only | no | `EditorOnlyJson` | Direct-play temp launch state; later cleanup may choose `Remove`, but this slice keeps it editor-only. |
| `Game.Feature.Stages.CampaignProfile.LegacyImportDisabled` | `CampaignLegacyImportMarkerStore` | yes | yes | test/reset cleanup only | no | `CampaignProfileJson` | Migration marker; should move into campaign profile metadata when the production switch is explicitly approved. |
| `Game.Feature.Stages.CampaignProfile.LegacyImportedSourceHash` | `CampaignLegacyImportMarkerStore` | yes | yes | test/reset cleanup only | no | `CampaignProfileJson` | Migration marker; binds retained PlayerPrefs source to profile import state. |
| `Game.Feature.Stages.CampaignProfile.LegacyResetTombstoneUtc` | `CampaignLegacyImportMarkerStore` | yes | yes | test/reset cleanup only | no | `CampaignProfileJson` | Migration marker; records reset tombstone in profile metadata. |
| `Game.Feature.Stages.CampaignProfile.LegacyDeletedSlotGuards` | `CampaignLegacyImportMarkerStore` | yes | yes | test/reset cleanup only | no | `CampaignProfileJson` | Migration marker; records deleted-slot resurrection guards in profile metadata. |
| `settings.audio.*` | `PlayerPrefsAudioSettingsStore` | yes | yes | no runtime delete | yes | `LocalSettingsJson` | Local user setting; excluded from campaign profile and Steam Cloud. |
| `settings.display.*` | `PlayerPrefsDisplaySettingsStore` | yes | yes | no runtime delete | yes | `LocalSettingsJson` | Local machine/display setting; excluded from campaign profile and Steam Cloud. |
| `Game.Feature.Input.KeyboardMovementScheme` | `PlayerPrefsKeyboardBindingStore` | yes | yes | no runtime delete | yes | `LocalSettingsJson` | Local input setting; excluded from campaign profile and Steam Cloud. |
| `Game.Feature.Input.KeyboardBindingOverridesJson` | `PlayerPrefsKeyboardBindingStore` | yes | yes | clear binding override only | yes | `LocalSettingsJson` | Local input setting; excluded from campaign profile and Steam Cloud. |
| `All1Shader*` | All In 1 Sprite Shader editor tooling | yes | yes | no runtime delete | no | `EditorOnlyJson` | Plugin editor preferences; excluded from campaign profile, Cloud, and release content. |
| `allIn1DefaultShader` | All In 1 Sprite Shader editor tooling | yes | yes | no runtime delete | no | `EditorOnlyJson` | Plugin editor preference; excluded from campaign profile, Cloud, and release content. |
| `Assets/` | All In 1 Sprite Shader editor tooling | write-only observed literal | yes | no runtime delete | no | `UnknownNeedsDecision` | Observed editor PlayerPrefs key literal in plugin code; needs owner decision before migration. |
| test-only dynamic keys | editor/UI/gameplay tests | yes | yes | test cleanup only | no | `Remove` | Prefix families such as `Game.Feature.Stages.Editor.Tests.*`, `Game.Feature.Stages.Tests.*`, `Game.Feature.UI.Tests.*`, `Game.Feature.Tests.*`, and `pending-launch-slot-provider-tests-*`; not production data. |

Target consistency:

- Campaign save keys target `CampaignProfileJson`.
- Pending launch has no key or JSON target. It remains application-session memory and must not be stored in LocalState or the Cloud profile.
- Audio, display, and input keys target `LocalSettingsJson` and must not be stored in the Cloud profile.
- Direct-play temp keys target `EditorOnlyJson` in this slice; a later explicit cleanup decision may move them to `Remove`.
- Old stage save keys target `DeleteOnlyLegacy`.
- Migration marker keys target `CampaignProfileJson`.
- Current campaign PlayerPrefs keys must be retained after a future switch as rollback/import source for a bounded period. PlayerPrefs key deletion is not part of this slice.

## Aggressive migration slice plan

| Slice | Purpose | Included keys | Risk | Rollback |
| --- | --- | --- | --- | --- |
| 1. Inventory freeze | Freeze all current PlayerPrefs keys/prefixes and add drift guards. | All keys and prefixes in the frozen inventory table. | Low; documentation and tests only. | Revert docs/tests; production behavior is unchanged. |
| 2. Campaign profile switch investigation | Validate `SaveSlotStore` call-site migration, profile write enablement, and adapter wiring on one revision before any production switch. | `Game.Feature.Stages.StageClearSaveSlots`, migration marker keys. | High; can affect campaign progression persistence. | Keep PlayerPrefs source retained and default constructor unchanged until switch approval. |
| 3. Local launch state schema | Keep installer-committed active separate from profile metadata and keep pending launch in application-session memory only. | `Saves/local-launch-state.json`, `Game.Feature.Stages.ActiveStageClearSaveSlot` import source; no pending key/file. | Medium; can affect continue/launch UX. | Retain the PlayerPrefs key as an import/diagnostic source while avoiding silent fallback from corrupt LocalState. |
| 4. Local settings schema | Move local settings into non-Cloud local JSON. | `settings.audio.*`, `settings.display.*`, `Game.Feature.Input.*`. | Medium; can affect user settings and machine-specific display config. | Keep PlayerPrefs reads as import fallback during transition. |
| 5. Editor/direct-play cleanup decision | Decide whether direct-play temp state becomes editor JSON or is removed. | `Game.Feature.Stages.DirectPlay.TempSaveSlots`, `Game.Feature.Stages.DirectPlay.TempActiveSaveSlot`. | Low; editor workflow only. | Keep current editor PlayerPrefs temp keys. |
| 6. Legacy tombstone cleanup | Remove retained legacy keys only after explicit retention-window closeout. | Delete-only legacy keys and test-only dynamic keys. | Medium; irreversible if users still need rollback/import. | Do not delete until import/rollback window and backups are closed. |

## SteamPipe content exclusion policy

SteamPipe release content staging is disabled for upload in this phase. No
SteamPipe VDF, depot config, or upload command is defined by this policy.

The default sanitizer strategy is include-list copy into a separate sanitized
staging root, for example:

- `Builds/Steam/Staging/Windows/`
- an equivalent clean directory outside the repository root and outside Unity
  project-generated roots

The repository root must not be used as SteamPipe content root. The Unity
project root, `Library/`, `TestLogs/`, `TestResults/`, `Logs/`,
`ProfilerCaptures/`, `obj/`, and `Temp/` must not be staging roots.

Include-list copy is the primary sanitizer strategy. Exclude-list cleanup is
only a secondary safety net after include-list copy and must not be the default
packaging strategy.

Windows x64 Unity release runtime include rules, implemented by
`WindowsDistributionStager` for generated release builds:

- `<Game>.exe`
- `<Game>_Data/**`
- `UnityPlayer.dll`
- `MonoBleedingEdge/**`, if Mono backend
- `GameAssembly.dll` and IL2CPP runtime files, if IL2CPP backend
- required managed assemblies, if generated
- required native plugins
- `StreamingAssets/**`, only if generated and production-required

Repository `Assets/`, `Docs/`, `Packages/`, `ProjectSettings/`, and `Tests/` are
not runtime include targets. The stager accepts the canonical product executable,
Unity runtime DLLs, `VectorQuake_Data/**`, and the generated Mono or IL2CPP runtime
tree. It then applies this document's deny list and the selected
`WindowsDistributionTargetPolicy` contract before promotion. Destination bytes,
not the source selection list, are the source of truth for the distribution
manifest and SHA-256 inventory.
The generated distribution manifest is the required runtime files manifest for
the promoted payload and is paired with the denied pattern scan and smoke launch.

Distribution output is external and transaction-scoped:

```text
<run-root>/
  payload/   # SteamPipe ContentRoot candidate; no manifest or SUCCESS marker
  evidence/  # distribution-manifest.json and SUCCESS.json
```

`Stage-WindowsDistribution.ps1` rejects an existing output root, repository-local
output, overlapping source/output paths, traversal paths, and reparse points. A
failed transaction never leaves a final `SUCCESS.json`. DirectWindows omits the
Steam native and managed binding; SteamWindows preserves both. Neither target ever
copies or creates `steam_appid.txt`.

SteamPipe release content staging must exclude:

- `steam_appid.txt`
- `TestLogs/`
- `TestLogs/SaveReadiness/`
- `TestResults/`
- `TestResult/`
- `Logs/`
- `ProfilerCaptures/`
- `CampaignProfileReadiness.md`
- `TestLogs/SaveReadiness/**`
- `campaign-save-seed.json`
- `Saves/profile.json`
- `Saves/profile.json.bak`
- `Saves/profile.*.tmp`
- `Saves/profile.json.corrupt.*`
- `Saves/profile.json.rejected.*`
- `Saves/profile.json.bak.rejected.*`
- `Saves/profile.reset.pending.json`
- `Saves/profile.reset.pending.json.bak`
- `Settings/local-settings.json`
- `Saves/local-launch-state.json`
- `Saves/editor-direct-play.json`
- `Saves/direct-play-temp.json`
- `*.pdb`
- `*.mdb`
- `*.log`
- `*.tmp`
- `Library/`
- `UserSettings/`
- `obj/`
- `Temp/`
- `.git/`
- `.github/`
- `.vs/`
- `Docs/`
- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `Tools/SteamPipe/**/cache`
- `Tools/SteamPipe/**/output`
- `Tools/SteamPipe/**/login`
- `Tools/SteamPipe/**/builder credentials/cache artifacts, if added later`

SteamPipe staging notes:

- Do not use the repository root directly as the SteamPipe staging source.
- Stage sanitized build output from an explicit release staging directory.
- SteamPipe VDF/depot upload remains disabled.
- Debug symbols are excluded until a separate shipping-symbol decision says otherwise.
- `campaign-save-seed.json` is a launch/import helper artifact and must not contaminate Cloud or SteamPipe release content.
- Readiness artifacts are CI artifacts only and must not contaminate release content.

## Steam API absence policy

This phase does not introduce:

- Steamworks.NET runtime integration.
- `SteamAPI` calls.
- `ISteamRemoteStorage`.
- `SteamRemoteStorage`.
- RemoteStorage save paths.
- Direct Steam Cloud API integration.

Steam Cloud readiness for this phase stops at the documented Auto-Cloud inventory draft and guard tests.
