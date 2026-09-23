# Four-Locale Static Typography and All-Resident Plan

Status: implemented; Windows Player admission passed, 2026-09-21.

## Decision

The four canonical locales are `en-US`, `ko-KR`, `ja-JP`, and `zh-CN`.
The production `GameplayUiTypographyTheme` directly references every shipping
font, material, and atlas. While the Theme is reachable, all four locale font
graphs remain resident. Font assets are not loaded or released per locale and
are not placed in locale-specific Addressables groups.

Unity Localization Locale and String Table Addressables remain locale-scoped.
That package lifecycle is independent from font residency.

## Font contract

- Japanese: Noto Sans JP Regular and Bold.
- Simplified Chinese: Noto Sans SC Regular and Bold.
- Korean: the existing KBO Dia Gothic Light/Medium pair remains in use. Both
  assets carry the same 301-scalar exact corpus built from printable ASCII,
  the `한국어` autonym, current Korean UI/Stage tables, and U+25A1.
- English: every font referenced by the Theme is Static and directly contains
  the governed English corpus plus the documented invariant ASCII and ellipsis
  set. English is not admitted through a locale-font union or another font's
  glyphs.
- Shipping atlases contain the exact approved current corpus, not a historical
  union. Generation iterates Unicode scalars.
- Cross-CJK fallback lists are empty. Missing governed content is a validation
  failure. TMP's missing-glyph marker remains U+25A1 `□`; when a style font
  does not contain that marker directly, it resolves from the TMP default
  LiberationSans asset. This marker path is not used as a content fallback.
- Orbitron ExtraBold's source face does not contain U+005E `^`. The current
  invariant set therefore excludes it, but an approved English source that
  introduces `^` still adds it to the governed corpus and makes regeneration
  fail. Unsupported production text is never silently delegated to another
  English font.
- Final atlas textures are non-readable. Atlas size, page count, and Player
  memory are recorded from the generated assets rather than assumed in advance.

## Approved-content chain

```text
English governed key/source change
→ Japanese and Simplified Chinese Draft translation
→ game-UI language pass
→ placeholder/tag/source validation
→ explicit human approval
→ read-only Theme/source-font/glyph-capacity preflight
→ production String Table merge
→ exact Unicode-scalar corpus
→ Static Atlas regeneration
→ Theme, glyph, residency, and UI validation
```

`Docs/Localization/Four-Locale-Translation-Draft.csv` is the review artifact.
Draft rows cannot be registered as ShipReady, written into production String
Tables, or used to generate shipping atlases.

The Unity Apply entry point repeats the governed-data checks even when invoked
without the Python wrapper. It pins all nine directly managed source-font file
hashes and validates each existing target's source linkage, generation settings,
Static/single-atlas/non-readable state, fallback state, and persistent identity.
It also packs each of the 11 governed font corpora into temporary,
non-persistent TMP assets before mutation, so a missing source glyph or
single-atlas overflow fails before production String Tables or font assets are
changed.

Apply accepts only the settled four-locale production topology. All four Locale
entries, all eight UI/Stage String Tables, both Shared Table Data entries, the
six localization Addressables groups, and their schemas must already match the
canonical paths, addresses, labels, and ownership. Apply validates this state
but never repairs it. Locale, Collection, Shared Data, Theme, and Addressables
assets are immutable inputs; only changed ja-JP/zh-CN String Tables and font
compound assets whose exact corpus changed may be written. Before mutation,
Apply rejects dirty main and embedded objects across the canonical managed
manifest and snapshots every managed asset plus its `.meta` as exact bytes.
The snapshot excludes Addressables because the bounded Addressables directory
transaction is its single rollback owner; that transaction also protects the
directory root `.meta`. It uses path-scoped
save/import instead of global SaveAssets/Refresh. A failure restores the set,
while success requires the complete Addressables file inventory and bytes to
remain unchanged and preserves every Theme font asset, material, and atlas
GUID/local-ID tuple. The separate KBO lane also requires both generated assets
to be byte-identical before and after a rebuild; recording hashes without
comparing them is not admission evidence.

## Residency evidence

The enabled Build Scenes directly reach the screen catalog, which directly
reaches the Theme. Admission must verify that every enabled Build Scene has the
Theme and all four locale font graphs in its transitive dependency closure.
No font handle owner, current/candidate slot, `Addressables.Release`,
`Resources.UnloadUnusedAssets`, or TMP registry cleanup is part of production.

Windows Player validation records the all-resident font/atlas memory delta and
checks all four locale switches for missing glyphs and first-use stalls. A
warm-up path is added only if Player evidence demonstrates a material hitch.

The 2026-09-21 fresh Windows Player admission passed on Direct3D 11 with the
following measured state:

- 11 distinct Theme font assets and 11 atlas textures remained resident across
  `en-US -> ko-KR -> ja-JP -> zh-CN -> en-US`.
- Every font was Static, single-atlas, fallback-free, and every atlas was
  non-readable.
- Atlas runtime memory was 30,413,632 bytes (about 29.0 MiB) before and after
  the locale sequence. The earlier readable Korean pair had measured about
  37.0 MiB; removing both CPU-readable copies removed about 8 MiB.
- All four locale selections and governed-corpus checks passed with zero
  per-font production glyph gaps. Four English style fonts omit U+25A1
  directly, but the diagnostic marker resolved from the TMP default
  LiberationSans asset with zero unresolved markers.

Evidence:
`/mnt/d/J2M/evidence/font-all-resident-admission/20260921T113041Z/player-probe-d3d/font-all-resident-player-report.json`.

## Admission boundary

All 142 current UI/Stage rows are Approved. Exact Japanese and Simplified
Chinese table coverage passes, both weights were regenerated from their exact
approved corpora, the Theme resolves every required style for all four locales,
and the Windows Player residency check passes. `ja-JP` and `zh-CN` are now
registered as ShipReady.

English has five role-specific style fonts. Each one must directly cover the
same governed production corpus; Player admission fails when any one font has a
missing production scalar. The only permitted resolution through the TMP
default asset is the visible U+25A1 `□` diagnostic marker itself.

## Approved Apply transaction hardening — 2026-09-22

The Apply transaction freezes a semantic manifest only after canonical
topology, source-font, clean-object, and current table checks pass. The manifest
stores persistent path/GUID/local-ID identities rather than live Unity object
references. For each UI/Stage collection it freezes the exact Shared Data
`(key, keyId)` map, English `(keyId, value, IsSmart)` inventory, Korean
`(keyId, value, IsSmart)` inventory, and approved Japanese/Chinese outputs.
Target writes use those frozen IDs and Smart states instead of rereading live
English during mutation.

After targeted save/import, admission now reloads the complete canonical
managed graph and requires every main object and embedded subasset to be clean.
It also requires immutable asset bytes, every managed `.meta`, all source-font
hashes, the frozen en-US/ko-KR/Shared Data semantics, approved ja-JP/zh-CN
semantics, font graph identities, and the complete Addressables directory plus
root `.meta` to remain exact.

Rollback is one bounded best-effort pass: clear dirty objects, restore only
changed bytes, import only affected asset paths, then verify all bytes and dirty
states. Per-path failures are accumulated. A callback that mutates a restored
asset again does not trigger an unbounded retry; rollback reports the workspace
unsafe so the Editor can be reopened or an isolated worktree discarded.

The same-working-tree `./run_tests.sh ui` run passed the Windows UI build and
Unity EditMode `1528 total / 0 failed`. The rollback fixture now proves managed
`.meta` restoration and rejection, while the bounded directory fixture proves
Addressables root `.meta`, changed, deleted, and added-file restoration.
Production Apply E2E fault injection and exact-SHA disposable-worktree CI were
not run in this change and are not claimed by this result.
