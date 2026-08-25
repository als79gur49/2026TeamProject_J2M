# Typography Visual QA Closeout

> Historical evidence note: this closeout predates the completed Climate Crisis
> KR migration. Nanum references below describe the captured revision and are
> not current asset-retention requirements; the canonical current state is
> defined by `Climate-Crisis-KR-Typography-Migration-Closeout.md`.

## Scope

This closeout records the visual QA result for the UI Localization + Typography migration PR scope, including the Settings static-shell completion and SmartFormat production-integration restoration. It does not approve package changes, TMP Settings fallback changes, Addressables remote setup, or broader UI typography rollout.

Evidence folder:

```text
TestLogs/TypographyVisualQA/CommandLine-20260725-154732/
```

This is the current production-composition corrected canonical 1920x1080 evidence generated through `./run_tests.sh typography-visual` from revision `bd9870b6e2e1b7e0b91b677a931416d714edb51b`. Its `capture.log` records schema 1, `RECONSTRUCTED_FROM_SPLIT_LOGS`, six PASS entries, clean guarded assets, and Settings `typography_bindings=38`, `localized_expected=22`, `localized_applied=22` for both locales. The wrapper verified every PNG byte size/SHA-256 and preserved the Nanum content/diff hashes.

The production Pause factory and screenshot utility now share `PausePopupProductionLocalizationComposer`, so both paths use the same static localization targets, locale resolver, `GameplayUiTypographyTheme`, `TypographyBinding` authored state, and binding lifetime. Runtime identity tests compare the Pause title, description, Resume, Retry, Settings, and Main Menu targets in en-US and ko-KR, including font/material GUID and file ID, font style, size, auto-sizing range, style tag, sizing source, and apply mask.

The canonical hierarchy is:

1. `TestLogs/TypographyVisualQA/CommandLine-20260722-210829/` — historical defective evidence containing SciFiSoldier Main Menu commands.
2. `TestLogs/TypographyVisualQA/CommandLine-20260724-214429/` — MainMenu-corrected canonical restoring the authored Orbitron identity.
3. `TestLogs/TypographyVisualQA/CommandLine-20260725-154732/` — current production-composition corrected canonical using the production Pause setup.

The current canonical is byte-identical to the MainMenu-corrected predecessor for all six PNGs. This is classified `BYTE_IDENTICAL`, not an expected-image update: the old capture setup bypassed production composition, while its later broad preview application happened to converge on the same rendered font/theme result. The false-green risk is closed by shared composition and runtime identity parity rather than by changing hashes. No unexpected visual delta was found. `TestLogs/TypographyVisualQA/CommandLine-20260720-194045/` remains unchanged as 51-binding historical evidence.

Manual review of the predecessor confirmed that Settings `W/A/S/D`, four directions, `E`, and `Q` keep the same physical-key presentation across en-US/ko-KR while Movement Keys, Use Arrow Keys, Push, Flip, Change, and Reset Input localize. No keycap/current-value clipping, wrapping, or 1920x1080 bounds issue was observed. Main Menu en-US remains intentionally changed from the historical incidental generic `Button` SciFiSoldier result to the authored Orbitron identity for Start, Settings, and Quit.

The corrected runtime uses the `MainMenuCommand` semantic role: en-US resolves Display/Bold to Orbitron ExtraBold while ko-KR uses the existing NanumGothic UI/Bold locale override. The generic `Button -> UI/Bold -> Font_SciFiSoldier_Bold` mapping is unchanged. Production-composition tests independently pin the origin/main authored font/material identity, Bold style, and `30 / Auto / 18-30` sizing through `en-US -> ko-KR -> en-US`.

An origin/main deterministic run of the current capture tooling is not available because that revision does not contain the same runner/capture implementation. Cross-revision pixel parity is therefore `NOT_AVAILABLE`; exact runtime font/material/sizing identity parity is `PASS`.

The repository wrapper refuses dirty P2 revisions and existing output directories, checks the current worktree/Unity path and active project process, writes raw Unity logs separately from `capture.log`, captures isolated slices, and retains failed output for diagnostics. A partial capture, Settings count mismatch, guarded-asset change, Nanum change, missing PNG, or hash mismatch fails the lane.

The 2026-07-24 correction-head UI lane produced `885/885` passed tests before the evidence guard was added. Final committed-head totals are recorded with the PR evidence.

## P0 Closeout

| Item | Result |
|---|---|
| Horizontal mirror | Resolved |
| `ko-KR` English-only rendering | Resolved |
| Pause placeholder title | Resolved |
| Pause / Main Menu `ko-KR` tofu | Resolved |
| Settings `ko-KR` authored English shell | Resolved |
| Production SmartFormat integration | Restored |
| Full UI lane | 854/854 PASS |

## Visual Fixes

| Item | Result |
|---|---|
| Settings Mute wrapping | Fixed |
| Settings audio/display static shell | Descriptor/String Table path complete |
| Pause description visibility | Fixed |

## Validation Alignment

| Evidence | Result |
|---|---|
| Localization integration | 23/23 PASS |
| Settings production runtime | 20/20 PASS |
| Typography | 44/44 PASS |
| UI Architecture | 58/58 PASS |
| Typography preview screenshot manifest | 2/2 PASS |
| Full UI | 854/854 PASS |
| Core EditMode | 197/197 PASS |
| Core PlayMode | 92/92 PASS |

## Smart Metadata Closeout

The final Smart metadata correction did not change localized copy, layout, runtime display output, or screenshot assets. Existing visual evidence remains valid.

Settings reset and display-preview confirmation popups are dynamic surfaces outside the six-image canonical. Their en-US, ko-KR, direct ko-KR open, and locale round-trip behavior is covered by production runtime tests and a raw-literal source guard.

| Check | Final state |
|---|---|
| Shared UI entries | 46 |
| Smart entries | 5 |
| Non-Smart entries | 41 |
| Violations | 0 |
| `ui.settings.input.rebind_canceled` | Static localized status; no runtime argument; non-Smart |

## Remaining

| Item | Decision |
|---|---|
| `ko-KR` synthetic bold/material polish | P2; optional and not required for this PR closeout |
| Screenshot evidence resolution | 1920x1080 |
| Broader Theme sizing | Not needed for this PR; Hybrid sizing remains the scoped policy |
| Optional bake | Not approved |
