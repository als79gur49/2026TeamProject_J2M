# Typography Visual QA Closeout

## Scope

This closeout records the visual QA result for the UI Localization + Typography migration PR scope, including the Settings static-shell completion and SmartFormat production-integration restoration. It does not approve package changes, TMP Settings fallback changes, Addressables remote setup, or broader UI typography rollout.

Evidence folder:

```text
TestLogs/TypographyVisualQA/CommandLine-20260722-210829/
```

This is the current canonical 1920x1080 evidence generated through `./run_tests.sh typography-visual` from revision `31b92cd2c9718e1a653da39a6db47c7a17ea7452`. Its `capture.log` records schema 1, `RECONSTRUCTED_FROM_SPLIT_LOGS`, six PASS entries, clean guarded assets, and Settings `typography_bindings=38`, `localized_expected=22`, `localized_applied=22` for both locales. The wrapper verified every PNG byte size/SHA-256 and preserved the Nanum content/diff hashes.

Manual review confirmed that Settings `W/A/S/D`, four directions, `E`, and `Q` keep the same physical-key presentation across en-US/ko-KR while Movement Keys, Use Arrow Keys, Push, Flip, Change, and Reset Input localize. No keycap/current-value clipping, wrapping, or 1920x1080 bounds issue was observed. Pause and Main Menu retain their historical bilingual capture behavior with no new Settings-policy regression. Print Screen and Numpad Enter are not visible in these frames, so their visual verification is not claimed.

`TestLogs/TypographyVisualQA/CommandLine-20260720-194045/` remains unchanged as historical evidence. Its Settings entries record 51, not the current exact 38 applied-binding contract; a dedicated test preserves that provenance without treating it as current canonical PASS.

The repository wrapper refuses dirty P2 revisions and existing output directories, checks the current worktree/Unity path and active project process, writes raw Unity logs separately from `capture.log`, captures isolated slices, and retains failed output for diagnostics. A partial capture, Settings count mismatch, guarded-asset change, Nanum change, missing PNG, or hash mismatch fails the lane.

The final 2026-07-22 UI lane produced `872/872` passed tests. Settings production localization runtime is 25/25 PASS, Settings production typography composition is 3/3 PASS, and typography fixtures are 55/55 PASS.

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
