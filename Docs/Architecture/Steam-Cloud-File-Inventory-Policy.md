# Steam Cloud File Inventory Policy

This policy freezes the Steam Cloud file inventory for Steam Release Phase B. It does not enable Steam Cloud, add Steamworks.NET, call Steam APIs, switch the production save backend, enable production `profile.json` writes, delete legacy PlayerPrefs keys, or define SteamPipe VDF/admin settings.

## Current production save truth

- Campaign save truth: `SaveSlotStore` backed by PlayerPrefs.
- Production save key: `Game.Feature.Stages.StageClearSaveSlots`.
- Pending launch / active slot key: `Game.Feature.Stages.ActiveStageClearSaveSlot`.
- Pending launch state is local/session only and is not a Steam Cloud target.
- Current V2 profile file: `Saves/profile.json`.
- `Saves/profile.json` is a future file-backed save target and diagnostics/readiness inventory input. It is not the current production canonical save.
- Current Steam Cloud action: Auto-Cloud application is deferred. Revisit after the production file-backed save switch, when `profile.json` is canonical production save.

## Company/Product path guard

- Unity Company: `J2M`.
- Unity Product: `VectorQuake`.
- Expected Windows persistent data path family: `AppData/LocalLow/J2M/VectorQuake`.
- Expected future save folder: `AppData/LocalLow/J2M/VectorQuake/Saves`.
- The Company/Product path `J2M/VectorQuake` must be frozen before applying Steamworks Auto-Cloud rules.
- Any pre-release Company/Product rename requires a Steam Auto-Cloud rule review before release content setup.

## Future Steam Auto-Cloud draft

Future only. Do not apply this rule until production saves are file-backed and `profile.json` is canonical production save.

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
- after production file-backed save switch
- after profile.json becomes canonical production save
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
- `campaign-save-seed.json`
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
- Backup clouding requires a separate tested backup-cloud policy before reconsideration.
- Direct-play temp save and temp active-slot keys are never Cloud targets.
- Readiness reports are CI artifacts only and are not Cloud targets.
- Audio, display, and input settings remain outside the campaign save Cloud inventory.

## SteamPipe content exclusion policy

SteamPipe release content staging must exclude:

- `steam_appid.txt`
- `TestLogs/`
- `TestResults/`
- `Logs/`
- `ProfilerCaptures/`
- `CampaignProfileReadiness.md`
- `TestLogs/SaveReadiness/**`
- `campaign-save-seed.json`
- `*.pdb`
- `*.mdb`
- `*.log`
- `*.tmp`
- `Library/`
- `UserSettings/`
- `obj/`
- `Tools/SteamPipe/**/cache`
- `Tools/SteamPipe/**/output`
- `Tools/SteamPipe/**/login`
- `Tools/SteamPipe/**/builder credentials/cache artifacts, if added later`

SteamPipe staging notes:

- Do not use the repository root directly as the SteamPipe staging source.
- Stage sanitized build output from an explicit release staging directory.
- Debug symbols are excluded until a separate shipping-symbol decision says otherwise.
- `campaign-save-seed.json` is a launch/import helper artifact and must not contaminate Cloud or SteamPipe release content.

## Steam API absence policy

This phase does not introduce:

- Steamworks.NET runtime integration.
- `SteamAPI` calls.
- `ISteamRemoteStorage`.
- `SteamRemoteStorage`.
- RemoteStorage save paths.
- Direct Steam Cloud API integration.

Steam Cloud readiness for this phase stops at the documented Auto-Cloud inventory draft and guard tests.
