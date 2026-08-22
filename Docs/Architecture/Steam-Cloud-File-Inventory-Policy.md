# Steam Cloud File Inventory Policy

Status: current inventory draft; Steam Cloud remains disabled

This policy does not enable Steam Cloud, add Steam APIs, delete local data, or
create Store artifacts.

## Current production persistence

- Campaign progression: `Saves/profile.json`
- Committed local active state: `Saves/local-launch-state.json`
- Pending launch: application-session memory only
- Running slot context: scene-local memory only
- PlayerPrefs campaign progression: unsupported
- Audio/display/input/locale PlayerPrefs settings: supported local settings
- Editor DirectPlay temporary state: isolated disposable JSON root

## Company/Product path guard

- Unity Company: `J2M`
- Unity Product: `VectorQuake`
- Windows path family: `AppData/LocalLow/J2M/VectorQuake`
- Canonical save directory: `AppData/LocalLow/J2M/VectorQuake/Saves`

Identity/version/build-number application is deferred to the release identity
milestone. Any identity change requires this inventory to be reviewed before a
Cloud rule is enabled.

## Future exact Auto-Cloud draft

Future only. Do not apply without a separate Steam Cloud enable decision.

```text
Root:
- WinAppDataLocalLow

Subdirectory:
- J2M/VectorQuake/Saves

Pattern:
- profile.json

Recursive:
- false
```

The include target is exactly
`WinAppDataLocalLow/J2M/VectorQuake/Saves/profile.json`. Do not use `*.json` and
do not enable recursion.

## Cloud exclusions

- `Saves/profile.json.bak`
- `Saves/profile.*.tmp`
- `Saves/profile.json.corrupt.*`
- `Saves/local-launch-state.json`
- `Saves/local-launch-state.json.bak`
- `Saves/local-launch-state.*.tmp`
- `Saves/local-launch-state.json.corrupt.*`
- `campaign-save-seed.json`
- `Settings/local-settings.json`
- editor DirectPlay isolated files
- audio/display/input/locale PlayerPrefs settings
- test, readiness, log, profiler, and debug-symbol artifacts

Profile backups and corrupt quarantine files are current local recovery
contracts, not Cloud inventory. Local launch-state backups remain excluded
diagnostic artifacts and are not an automatic fallback source. Reconsidering
backup clouding requires a separate tested policy.

## PlayerPrefs boundary

| Category | Production use | Cloud target | Reset policy |
| --- | --- | --- | --- |
| Campaign progression | unsupported | no | exact legacy keys only, after approval |
| Audio settings | supported | no | preserve |
| Display settings | supported | no | preserve |
| Input settings | supported | no | preserve |
| Locale setting | supported | no | preserve |
| Editor DirectPlay campaign data | no PlayerPrefs use | no | delete isolated files only |
| Unknown keys | owner decision required | no | do not delete |

Broad deletion of `HKCU\\Software\\J2M\\VectorQuake` is forbidden.

## Release artifact boundary

Historical Store artifacts are immutable private evidence. They are not current
release-pipeline input and must not be repackaged or promoted. No Store artifact
is required for the pre-release save baseline reset.

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
manifest and SHA-256 inventory. The generated distribution manifest is the
required runtime files manifest and is paired with the denied pattern scan and
smoke launch.

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
- `Saves/local-launch-state.json.bak`
- `Saves/local-launch-state.*.tmp`
- `Saves/local-launch-state.json.corrupt.*`
- isolated Editor/Player DirectPlay campaign files
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
