# Climate Crisis KR Typography Migration PR2 Closeout

## Scope and decision

This document is the current closeout for Climate Crisis KR typography PR2. It
supersedes Nanum-based current-state wording in earlier typography planning and
PR1 visual documents, while retaining those documents and assets as historical
evidence.

The governed production surfaces are Settings, Pause, Main Menu, HUD, terminal
results, and ConfirmPopup typography. The current policy is:

- `THREE_TIER_CLIMATE`: the 3 Display roles use `ClimateCrisisKR-2000`; the 4
  Heading and 5 Body roles use `ClimateCrisisKR-2019`; the remaining 7 UI and
  Utility roles use `ClimateCrisisKR-2000` in `ko-KR`.
- The resolved split is 9 roles on 2019 and 10 roles on 2000. No new role or
  font category is introduced.
- `PRESERVE_AUTHORED_SIZE`: locale rules do not own font size, Auto Size, its
  min/max range, line spacing, or character spacing.
- `SettingsStatus` remains `Hybrid / PreserveAuthored`, `fontSize 14`, Auto
  Size on, range `10-14`, and authored height `28`. Two Korean lines are
  intentional when there is no clipping or overflow.
- `en-US` retains the serialized base/canonical identities. Generic Button
  remains SciFiSoldier and MainMenuCommand remains Orbitron.

## Font provenance and asset identity

The font is **Climate Crisis KR / 기후위기-한글**, distributed under the
NohType name. The repository TTF metadata records copyright 2022 NohType,
design credits, and “SIL Open Font License, Version 1.1.” The SandollCloud
distribution page identifies NohType, the 2022 release, designers Noh Eunyou
and Lee Joohee, the Climate Crisis KR filename, and the supported use scope:

- <https://www.sandollcloud.com/font/17665/Climate-Crisis-KR>

`Assets/_Shared/UI/Fonts/기후위기-한글_사용설명서.pdf` is a usage guide. It
is not described or treated as standalone license text.

| Identity | Climate 2000 | Climate 2019 |
|---|---|---|
| TTF GUID | `5360535d0de75234ca21822297323672` | `56e1f07e315e49a4a8e5043a11e04e29` |
| TTF SHA-256 | `aa0e58ef1dd54ae760c29bdd0ce28d6b710c2d5910e88efadf5e23416b01d0f1` | `48e723743cd5c162ba5efb6f560f179e3dc7b56bacc5c72ffe6ac4be0c8660f3` |
| SDF GUID | `40d61154fd6576b4d85c2d78460b16ad` | `7dfd9aae81fc1d242b007a3b7a042fb0` |
| SDF material local ID | `1352911973252649374` | `7808543287137721147` |
| SDF atlas local ID | `-2536001923755311345` | `-5757234995057936259` |
| Candidate SDF SHA-256 | `66193afe72fe9e2c4de11596ed68c1eb038fffb7f0f71b8605669f68922c459f` | `6e2a0b412311d1942cc2d332c2af7892a1d3c48a094019e1e3e54f07ef91ace0` |
| Atlas/fallback | static single atlas; fallback empty | static single atlas; fallback empty |

Committed source identity and Unity-loaded state are separate contracts.
`run_tests.sh` verifies the `HEAD` Git blob, GUIDs, local ID, source TTF, and
Nanum retention before Unity starts. Unity tests verify the loaded font and
material references, static atlas, native glyph coverage, empty fallback,
theme-role mapping, and layout/rendering behavior.

The working-file shape with SHA-256
`71ae00a952cf086150c90764db323bf078bf871e133ce52844cc1c94070d6445`
and derived values `ScaleRatioA=0.9`, `ScaleRatioC=0.73125` is recorded only as
`EXPECTED_IMPORT_DERIVED_DRIFT`. It is not a committed source identity,
production input contract, or commit candidate. A working hash outside the
committed and known derived shapes is `UNEXPECTED_IMPORTER_MUTATION`; any
glyph, atlas, reference, fallback, or unexplained pixel change remains a
blocker.

## Role, glyph, and retention governance

- Base roles: `19`; en-US resolved roles: `19`; ko-KR resolved roles: `19`.
- Missing or duplicate resolved roles: `0`.
- Heading/Body roles resolve to 2019; Display/UI/Utility roles resolve to 2000.
  Every ko-KR role keeps Normal style and a sizing mask that does not own
  authored sizing.
- Managed `*_ko-KR.asset` String Tables are scanned dynamically. The closeout
  set contains 66 values, 65 distinct values, and 116 distinct non-ASCII
  codepoints; missing native Climate glyphs and fallback dependencies are `0`.
- The Nanum TTF/meta, SDF/meta, and SyntheticBold material/meta remain tracked.
  Retention is independent from the fact that current ko-KR role mapping no
  longer resolves to Nanum.

## Layout correction

| Target | Before | Final contract | Result |
|---|---|---|---|
| `PausePopup/Title` | width `82.02`, x `158.99`, two Korean lines | width `160`, x `120`, height `40`, anchor/pivot unchanged | visual center remains exactly `200`; `일시 정지` is one line |
| Settings Main/Bgm/Sfx `Value` | Rect width and preferred width `100` | Rect width and preferred width `140`, height `32` | `0/25/50/75/100%` muted/unmuted values are one line |
| Settings `DisplayStatus` | height `28` | unchanged: height/preferred height `28`, size `14`, Auto Size `10-14` | two Korean lines allowed; no clipping/overflow |

No theme sizing rule, font size, Auto Size flag/range, semantic role, runtime
resolver, font asset, or Nanum asset was changed for the layout correction.

## Automated evidence

Code-head evidence before this documentation-only closeout:

- revision: `840a5cd2fe0c1460a0a47fc34d8e020f73c76abf`
- Core EditMode: `197/197`
- Core PlayMode: `92/92`
- UI EditMode: `1060/1060`
- focused Climate/Theme/Typography/Settings/Pause/Main Menu/Localization
  aggregation: `167/167` at the governance head, plus the later dedicated
  diagnostic capture test `1/1`
- project-wide: `NOT_RUN`; the documented broad baseline remains red and no
  project-wide green claim is made

The shell preflight pins the committed asset GUID/material/blob identity and
Nanum retention. Unity governance tests pin runtime references, theme
completeness, en-US serialized theme payload identity, glyph coverage,
fallback absence, and the three approved layout contracts. Import-derived
scale ratios and the post-import working-file hash are diagnostics, not Unity
source-integrity assertions.

## Visual evidence

The following canonical candidate predates the 2019 hierarchy change and is
retained as historical evidence:

```text
TestLogs/TypographyVisualQA/CommandLine-20260726-052954/
```

Its schema-1 `capture.log` records revision
`840a5cd2fe0c1460a0a47fc34d8e020f73c76abf`, `1920x1080`,
`RECONSTRUCTED_FROM_SPLIT_LOGS`, overall `PASS`, six canonical entries,
Settings `38` typography bindings and localized `22/22` for both locales,
empty errors, verified PNG byte size/SHA-256, and preserved Nanum state.

`Diagnostics/` is deliberately outside the exact six-file canonical root and
contains:

- `SettingsAudioMuted_ko-KR.png`:
  `0b4518f0c6ede9bae2d46f6687ac245821a2da96279dc2025275c75b8aa2a902`
- `SettingsDisplayStatus_ko-KR.png`:
  `1667019c1f7089ec04921f9041a2d10cd736c073c80bee19cd23c2b3a706f2ca`
- `ConfirmPopup_ko-KR.png`:
  `d7e37f8c745c9f557f98df22da662faccdec737daf9cf03e2819787a1bc6dc6b`

Against `CommandLine-20260725-154732`, Settings en-US and Main Menu en-US are
byte-identical. Pause en-US is `EXPECTED_LAYOUT_DELTA`: its approved width
increase changes the Auto Size render result while the authored visual center,
alignment, font identity, font size setting, Auto Size flag/range, and one-line
flow remain fixed. It is not an en-US typography identity regression.

Generated final evidence is not committed because adding it would create a new
revision and immediately invalidate `manifest git_head == HEAD`. The final
same-revision output path and hashes belong in PR evidence; historical
canonical folders are retained and never overwritten.

## Known limitations and follow-up boundary

- Display status intentionally permits two lines; forcing one line or height
  `48` is a contract regression.
- This closeout does not expand typography to ungoverned future UI surfaces.
- No global font scaling, package/TMP Settings fallback change, Nanum deletion,
  PR creation, or merge is authorized here.
- Independent current-head audit remains required before opening the PR.
