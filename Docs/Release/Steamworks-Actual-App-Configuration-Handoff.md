# Steamworks Actual App Configuration Handoff

## Scope and current status

This handoff records repository expectations and the actual app/depot IDs supplied by the owner on 2026-09-06. The IDs have not been independently verified in App Admin. This document does not record an App Admin change or a published Steam schema.

- Actual AppID: `5218360` (owner supplied)
- Actual Windows DepotID: `5218361` (owner supplied)
- App/depot identity status: `OWNER_SUPPLIED_PENDING_APP_ADMIN_VERIFICATION`
- Achievement publication status: `EXPECTED_NOT_PUBLISHED`
- Steamworks App Admin: `NOT_CHANGED`
- SteamCMD: `NOT_RUN`
- SteamPipe upload: `NOT_PERFORMED`

The machine-readable expectation report is generated from typed repository contracts and remains identity-free. The owner-supplied IDs above are deployment inputs, not runtime constants or proof of Steam publication.

## Current build and PowerShell upload procedure (2026-09-06)

- Source commit: `2af3a38fa3e63c3ccda97b59958b300349a04247`.
- Steam Windows payload: `D:\J2M\builds\steam-20260906\upload-2af3a38fa\payload`.
- Build and distribution validation passed: 0 build errors, 2 warnings; 249 promoted files; required Steam native and managed bindings present.
- Actual Steam upload and account unlock verification have not been performed by this workflow.
- SteamCMD found on this machine: `D:\Downloads\steamworks_sdk_165\sdk\tools\ContentBuilder\builder\steamcmd.exe`.
- Upload configuration: `D:\J2M\builds\steam-20260906\app_build_5218360.vdf`.
- The upload configuration targets AppID `5218360` and DepotID `5218361`, maps the payload recursively to the depot root, and omits `SetLive`. Its `Preview` value is `0`, so executing it performs a real upload. The repository dry-run tool's immutable preview files remain separate.

Open an ordinary PowerShell window in any directory. Copy these lines; `Read-Host` asks for the Steam login account name, not the profile display name:

```powershell
$steamCmdPath = 'D:\Downloads\steamworks_sdk_165\sdk\tools\ContentBuilder\builder\steamcmd.exe'
$uploadVdfPath = 'D:\J2M\builds\steam-20260906\app_build_5218360.vdf'
$steamUploadAccount = Read-Host 'Steam login account name'
& $steamCmdPath +login $steamUploadAccount +run_app_build $uploadVdfPath +quit
```

Enter the password and Steam Guard verification in the SteamCMD prompts when requested. The account needs upload permissions for this app. The `&` operator launches the executable at the quoted full path; no directory change or administrator PowerShell is required by these commands.

After successful upload, open [App 5218360 Builds](https://partner.steamgames.com/apps/builds/5218360), identify the new Build ID, and apply it to the intended test branch. Uploading alone does not select the build for a branch. Ensure the test account's package includes the app and Depot `5218361`. In Steamworks launch options, configure `VectorQuake.exe` with `-j2mPlatformProvider steam`; install/update that branch from the Steam client.

The five achievement definitions must separately be created and published in this same AppID before testing unlocks. Build upload does not create achievements. A successful upload/build is not proof of successful achievement publication.

Reference: [Valve SteamPipe upload instructions](https://partner.steamgames.com/doc/sdk/uploading).

## Generate the expectation report

Run the Editor exporter from a clean committed worktree or detached committed checkout and write output outside the repository. The wrapper holds the worktree index, branch-ref or detached-HEAD lock, and read locks for tracked source files throughout Unity execution. It fails closed when staged, unstaged, or untracked changes are present and revalidates HEAD/tree after Unity exits. Both KBO font assets are excluded from source locking and checked for byte convergence after Unity exits; any font or other concurrent source change is preserved and fails the run:

```powershell
Tools\Release\Export-SteamworksConfigurationExpectation.ps1 `
  -RepositoryRoot <J2M_WORKTREE_PATH> `
  -OutputRoot D:\J2M\evidence\Steamworks-Configuration-Expectation-<UTC>
```

The exporter reads the `steam-windows` contract, `SteamAchievementMapping.Production`, `GameAchievementCatalog.Production`, and Unity Player Settings through typed APIs. The canonical JSON has no timestamp, AppID, DepotID, account, or credential field. Its companion SHA-256 file records the byte hash.

## Repository expectation

### Launch option

- Distribution target: `steam-windows`
- Executable: `VectorQuake.exe`
- Arguments: `-j2mPlatformProvider steam`
- Operating system: Windows
- Expected provider: `steam`

This is a repository expectation. It does not mean the Steamworks General Installation launch option has been configured or published.

### Product achievements

| Product Achievement ID | Expected Steam API Name | Unlock condition | Status |
| --- | --- | --- | --- |
| `campaign.level-0.clear` | `VQ_LEVEL_0_CLEAR` | Normal Level0 last-stage clear (currently `stage-0-3`) | `EXPECTED_NOT_PUBLISHED` |
| `campaign.level-1.clear` | `VQ_LEVEL_1_CLEAR` | Normal Level1 last-stage clear (currently `stage-1-2`) | `EXPECTED_NOT_PUBLISHED` |
| `campaign.level-2.clear` | `VQ_LEVEL_2_CLEAR` | Normal Level2 last-stage clear (currently `stage-2-2`) | `EXPECTED_NOT_PUBLISHED` |
| `campaign.level-3.clear` | `VQ_LEVEL_3_CLEAR` | Normal Level3 last-stage clear (currently `stage-3-3`) | `EXPECTED_NOT_PUBLISHED` |
| `campaign.level-4.clear` | `VQ_LEVEL_4_CLEAR` | Normal Level4 last-stage clear (currently `stage-4-3`) | `EXPECTED_NOT_PUBLISHED` |

All five exclude DirectPlay and Force Clear and have no Push/Flip count limit. Last-stage identity comes from the serialized campaign sequence per level group. Hidden recommendation is `false` for owner review. The previous three pre-release achievements are retired without unlock migration; their local earned/pending IDs may remain as inactive unknown records and are neither published nor converted.

The runtime records qualifying normal-stage performance in the campaign save before it
attempts Product Achievement earning. All achievements produced by that committed fact are
written to the product ledger atomically and published as one Steam batch: eligible locked
items are set first and one `StoreStats` follows. Exact-name `UserAchievementStored_t`
callbacks confirm individual items. Generic `UserStatsStored_t` results are diagnostic only
because they contain no achievement identity. `StoreStats(false)` fails the current store
candidates but allows the next queued batch to start. A 30-second named-callback timeout
preserves named successes, fails unresolved items, quarantines the publisher session, and
makes queued batches unavailable. Pending records remain durable for startup recovery when
a later process attaches a healthy Steam publisher; no replacement publisher is attached in
the same application lifetime. Runtime readiness is evaluated once as each batch enters
mutation. Readiness loss returns unavailable, while an exception from `GetAppId`,
`IsSteamIdValid`, or `IsLoggedOn` returns failed; both quarantine before any Achievement API
call. Achievement schema, pre-read, set, or store exceptions quarantine at their point of
failure, preserve exact item results already known, fail unresolved items, and make queued
batches unavailable. Publisher disposal completes every active and queued item as unavailable,
regardless of any partial result accumulated by the active batch. A callback-pump exception also
detaches and disposes Product publication immediately while leaving native shutdown to the normal
runtime shutdown path; pending records remain durable.

Owner-review copy drafts:

| Field | English draft | Korean draft | Status |
| --- | --- | --- | --- |
| Display name | Campaign Complete | 캠페인 완료 | `DRAFT_FOR_OWNER_REVIEW` |
| Description | Complete the VectorQuake campaign through normal gameplay. | 정상 플레이로 VectorQuake 캠페인을 완료했습니다. | `DRAFT_FOR_OWNER_REVIEW` |

These drafts are not canonical code or localization-table values.

Icon preparation checklist:

- Unlocked icon: `PENDING_ASSET`
- Locked icon: `PENDING_ASSET`
- Source art ownership: `PENDING_CONFIRMATION`
- Final Steam upload format: `VERIFY_IN_APP_ADMIN`

No icon is generated or uploaded in this phase. Confirm the current App Admin requirements for AppID `5218360` before uploading achievement artwork.

## SteamPipe local dry-run handoff

`Tools/SteamPipe/Prepare-SteamPipeBuild.ps1` generates deterministic preview VDFs
and validates them locally. It does not start SteamCMD, contact Steam, perform an
upload, or change App Admin state. Valve's SteamPipe `Preview` setting describes a
real build preview only when an authorized operator later submits the VDF through
SteamCMD; this repository tool only prepares and validates those files.

Official reference: [Uploading to Steam (SteamPipe)](https://partner.steamgames.com/doc/sdk/uploading)

The input must be an already promoted `steam-windows` artifact:

```text
<PROMOTED_STEAM_WINDOWS_ROOT>/
  payload/
  evidence/
    SUCCESS.json
    distribution-manifest.json
```

The generated `ContentRoot` is exactly `payload/`; release evidence is never mapped
into the Depot. `OutputRoot` and its separate `BuildOutput` must be outside both the
repository and the promoted payload. The tool reuses the typed Windows distribution
validator to compare every payload path, size, and hash with the manifest and to
enforce the canonical SteamWindows native/managed binding and deny contracts.
All repository, promoted-artifact, output, validator-cache, and loaded-validator
paths must resolve through verified local fixed, removable, or RAM drives. UNC,
device, mapped-network-drive, and reparse-point paths fail before content access.

Current synthetic validation form:

```powershell
Tools\SteamPipe\Prepare-SteamPipeBuild.ps1 `
  -AppId <NON_PRODUCT_SYNTHETIC_APP_ID> `
  -DepotId <NON_PRODUCT_SYNTHETIC_DEPOT_ID> `
  -PromotedSteamWindowsRoot <PROMOTED_STEAM_WINDOWS_ROOT> `
  -OutputRoot D:\J2M\evidence\SteamPipe-PreAppId-DryRun-<UTC> `
  -IdentityMode Synthetic `
  -DryRun
```

Synthetic output is `SYNTHETIC_VALIDATION_ONLY`, `NOT_UPLOADABLE`,
`NOT_ACTUAL_STEAM_IDENTITY`, `NO_STEAM_BACKEND_CONTACT`, and
`NO_APP_ADMIN_CONFIGURATION`. The placeholders above are external test inputs, not
stored App or Depot identities.

Future Actual-identity local validation form, only after the owner supplies both
identities:

```powershell
Tools\SteamPipe\Prepare-SteamPipeBuild.ps1 `
  -AppId 5218360 `
  -DepotId 5218361 `
  -PromotedSteamWindowsRoot <PROMOTED_STEAM_WINDOWS_ROOT> `
  -OutputRoot D:\J2M\evidence\SteamPipe-Actual-Identity-DryRun-<UTC> `
  -IdentityMode Actual `
  -DryRun
```

Actual mode rejects AppID 480. It still performs only a local dry-run and does not
grant upload authority. Credentials, SteamID, account details, branch activation,
`SetLive`, and SteamCMD execution are intentionally outside the command surface.

## Actual-App owner checklist

1. Verify owner-supplied VectorQuake AppID `5218360` in App Admin.
2. Confirm App Admin access for the authorized owner account.
3. Create exactly `VQ_LEVEL_0_CLEAR`, `VQ_LEVEL_1_CLEAR`, `VQ_LEVEL_2_CLEAR`, `VQ_LEVEL_3_CLEAR`, and `VQ_LEVEL_4_CLEAR` with exact ordinal API Names.
4. Review and enter Display Name, Description, Locked/Unlocked icons, and Hidden setting for each achievement.
5. Publish the Steamworks changes.
6. Register the Windows launch option for `VectorQuake.exe` with `-j2mPlatformProvider steam`.
7. Verify Windows Depot `5218361` belongs to AppID `5218360`.
8. Include the Depot in the Developer Comp Package.
9. Use the owner-supplied IDs as deployment inputs; keep credentials out of source and configuration files.
10. Compare App Admin settings with a report generated from the release revision.
11. Generate a SteamPipe preview VDF with the local dry-run tool.
12. Perform an actual SteamPipe preview only after separate approval.
13. Upload only after separate explicit approval.
14. Install the resulting private branch build from the Steam Library.
15. Start with a clean QA account (or an approved partner-side achievement reset) and clean local `profile.json` / `achievements.json` test state; do not add a reset API to the product runtime.
16. Normally clear each level's last stage (`stage-0-3`, `stage-1-2`, `stage-2-2`, `stage-3-3`, `stage-4-3`). Verify only its matching `VQ_LEVEL_n_CLEAR` unlocks and remains locally pending for this application lifetime.
17. Fully exit and restart the game. Verify already-unlocked Steam pre-reads remove matching pending IDs; scene reload, Main Menu entry, and Stage retry are not confirmation restarts.
18. Verify intermediate-stage clears, Force Clear, and every Editor DirectPlay mode grant no new level achievement. Verify a normal last-stage clear above 25 Push/Flip uses still qualifies.
19. Verify re-clears do not duplicate awards. Seed only the three retired pre-release earned/pending IDs in a QA ledger and verify they remain unchanged without startup cleanup writes, publication, or replacement grants; a receipt-only Campaign save must also grant nothing.


## Remaining deployment inputs and verification

- App Admin verification of owner-supplied AppID `5218360` and Windows DepotID `5218361`
- Package and branch decision
- Confirmation of an authorized Steamworks account
- Final achievement copy approval
- Locked and unlocked icon assets

`PUBLISHED_VERIFIED` may be used only after the achievement exists in the correct App Admin, the changes are published, and the actual configuration has been verified. Local repository validation alone is never `PUBLISHED_VERIFIED`.
