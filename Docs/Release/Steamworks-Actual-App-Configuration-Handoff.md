# Steamworks Actual App Configuration Handoff

## Scope and current status

This handoff records repository expectations for the future VectorQuake Steamworks App Admin setup. It does not record an App Admin change or a published Steam schema.

- Actual AppID: `NOT_CONFIGURED`
- Actual Windows DepotID: `NOT_CONFIGURED`
- App/depot identity status: `ACTUAL_IDENTITY_NOT_CONFIGURED`
- Achievement publication status: `EXPECTED_NOT_PUBLISHED`
- Steamworks App Admin: `NOT_CHANGED`
- SteamCMD: `NOT_RUN`
- SteamPipe upload: `NOT_PERFORMED`

The machine-readable report is generated from typed repository contracts. Do not copy these values into a second JSON source or infer an actual Steam identity from examples.

## Generate the expectation report

Run the Editor exporter from a clean committed worktree or detached committed checkout and write output outside the repository. The wrapper holds the worktree index, branch-ref or detached-HEAD lock, and read locks for tracked source files throughout Unity execution. It fails closed when staged, unstaged, or untracked changes are present and revalidates HEAD/tree after Unity exits. Known Unity font-import normalization is excluded from source locking and restored only on an exact match; any other concurrent change is preserved and fails the run:

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

### Product achievement

- Product Achievement ID: `campaign.complete`
- Expected Steam API Name: `VQ_CAMPAIGN_COMPLETE`
- Current status: `EXPECTED_NOT_PUBLISHED`
- Unlock condition: normal Campaign final Objective clear
- Excluded paths: DirectPlay and Force Clear
- Hidden recommendation: `false`

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

No icon is generated or uploaded in this phase. Confirm the current App Admin requirements after the Actual AppID is available.

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
  -AppId <ACTUAL_APP_ID> `
  -DepotId <ACTUAL_DEPOT_ID> `
  -PromotedSteamWindowsRoot <PROMOTED_STEAM_WINDOWS_ROOT> `
  -OutputRoot D:\J2M\evidence\SteamPipe-Actual-Identity-DryRun-<UTC> `
  -IdentityMode Actual `
  -DryRun
```

Actual mode rejects AppID 480. It still performs only a local dry-run and does not
grant upload authority. Credentials, SteamID, account details, branch activation,
`SetLive`, and SteamCMD execution are intentionally outside the command surface.

## Actual-App owner checklist

1. Confirm the Actual VectorQuake AppID.
2. Confirm App Admin access for the authorized owner account.
3. Create the `VQ_CAMPAIGN_COMPLETE` achievement.
4. Review and enter Display Name, Description, Locked/Unlocked icons, and Hidden setting.
5. Publish the Steamworks changes.
6. Register the Windows launch option for `VectorQuake.exe` with `-j2mPlatformProvider steam`.
7. Confirm or create the Windows Depot.
8. Include the Depot in the Developer Comp Package.
9. Record the Actual Windows DepotID in private release input, not canonical source.
10. Compare App Admin settings with a report generated from the release revision.
11. Generate a SteamPipe preview VDF with the local dry-run tool.
12. Perform an actual SteamPipe preview only after separate approval.
13. Upload only after separate explicit approval.
14. Install the resulting private branch build from the Steam Library.
15. Complete the Campaign normally.
16. Verify the actual achievement unlock and pending-publication removal behavior.

## Inputs required after AppID assignment

- Actual AppID
- Windows DepotID
- Package and branch decision
- Confirmation of an authorized Steamworks account
- Final achievement copy approval
- Locked and unlocked icon assets

`PUBLISHED_VERIFIED` may be used only after the achievement exists in the correct App Admin, the changes are published, and the actual configuration has been verified. Local repository validation alone is never `PUBLISHED_VERIFIED`.
