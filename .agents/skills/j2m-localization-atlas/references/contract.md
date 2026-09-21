# Four-locale contract

## Locale lifecycle

- Stable order: `en-US`, `ko-KR`, `ja-JP`, `zh-CN`.
- Autonyms: `English`, `한국어`, `日本語`, `简体中文`.
- `ja-JP` and `zh-CN` remain Draft until every governed UI and Stage row is approved and all admission checks pass.
- Production uses exact-locale lookup. There is no English string fallback and no cross-CJK font fallback.

## Translation truth

- English production String Tables define the current key set and source text.
- `Docs/Localization/Four-Locale-Translation-Draft.csv` is the review artifact, not a production runtime asset.
- Allowed review states are `Draft` and `Approved`.
- Any English source change invalidates the corresponding approval.
- Placeholders such as `{0}`, TMP tags, newlines, identifiers, and meaningful numbers must be preserved.

## Font truth

Japanese and Simplified Chinese each use independent Regular and Bold source fonts under `Assets/_Shared/UI/Fonts/NotoSansCJK`.

| File | SHA-256 |
| --- | --- |
| `NotoSansJP-Regular.otf` | `dff723ba59d57d136764a04b9b2d03205544f7cd785a711442d6d2d085ac5073` |
| `NotoSansJP-Bold.otf` | `1b0edfb500b73a4fa8a4fcaae1bbbd403994e08e73e3e0da37e70d3853f42c5f` |
| `NotoSansSC-Regular.otf` | `faa6c9df652116dde789d351359f3d7e5d2285a2b2a1f04a2d7244df706d5ea9` |
| `NotoSansSC-Bold.otf` | `c6cb5a93abaa9edc8ee7463b7ebb7f42d618d40e6ed2f7a5371c97b0b64767c0` |
| `Exo2.0-Regular.otf` | `dca1f9e0702c15641a26d5616ecbb87f7f6c12e5604b03fcf086c1155b9b936d` |
| `Exo2.0-SemiBold.otf` | `2cb43389c39ca1fb2ce07d40b68bc14a166d355dfde0d6fcf9c92101a1a26d2a` |
| `SairaCondensed-SemiBold.ttf` | `30f8ed4d078211003a9715c80c51ce031bab5c9a17e8771182e4c4599205634b` |
| `Orbitron-ExtraBold.ttf` | `e2694ab08e4a1d120e495107b2b12d753372fa5cf569c8864a55422bbbb0580e` |
| `LiberationSans.ttf` | `e5b0af421ea2bfbc1ac8d251d647268087ae82786234c57f757d1f0b90fa8b49` |

- License: SIL Open Font License 1.1, stored beside the fonts as `OFL-1.1.txt`.
- Shipping TMP assets are Static, single-atlas unless a reviewed budget change says otherwise, have no locale fallback fonts, and use non-readable atlas textures after generation.
- Corpus is rebuilt from the approved current strings, the locale autonym, and the documented invariant/runtime argument character set. Do not union the old character table into the new corpus.
- Iterate Unicode scalars, not UTF-16 code units.
- Every English Theme font is Static and directly contains the governed English corpus plus the documented invariant ASCII and U+2026 ellipsis set. English has no locale-font-union admission exception, and one style font may not supply another style font's production glyphs.
- Orbitron ExtraBold's source face does not contain U+005E `^`. The invariant set excludes it, but an approved English source containing `^` must make generation fail rather than fall back.
- TMP's U+25A1 `□` missing-glyph marker may resolve from the TMP default LiberationSans asset. This exception is limited to the diagnostic marker and cannot satisfy governed production content.
- Existing Korean Light/Medium assets remain the Korean regular/bold semantic pair. Both are rebuilt from one exact corpus: printable ASCII, the `한국어` autonym, current Korean UI/Stage String Tables, and U+25A1. Neither asset may retain a historical character-table union.

## Residency

- Font assets are direct Theme dependencies, not per-locale Addressables.
- All four locale font graphs remain resident while the Theme is reachable.
- Unity Localization Locale and String Table Addressables remain locale-scoped; they are unrelated to font residency.
- Do not introduce font handles, current/candidate font slots, font release, `Resources.UnloadUnusedAssets`, or TMP registry cleanup.

## Approval boundary

Draft validation and preview generation are reversible authoring work. Writing production tables, registering locales as ShipReady, updating the Theme, or replacing shipping atlases requires explicit approval of the affected Draft rows.

The approved Apply entry point independently validates the CSV schema, exact English inventory/source match, approval state, structural tokens, Theme mapping, source-font identity and hashes, existing target settings and persistent identities, glyph supply, and single-atlas fit before its first production mutation. It only accepts the settled four-locale topology: every Locale, UI/Stage String Table, Shared Table Data entry, localization Addressables group, and group schema must already match its canonical path, address, labels, and ownership. Apply must not create or move a Locale or Table, rewrite the Theme, or repair Addressables labels, groups, entries, or schemas.

Before mutation, Apply rejects unsaved changes on every main object and embedded sub-asset in the canonical managed manifest, including font materials and atlas textures. It then snapshots every managed production asset as exact bytes. Apply is a delta writer: it may dirty only changed ja-JP/zh-CN String Tables and font compound assets whose exact corpus changed, saves only those paths, and imports only those paths. Locale, Collection, Shared Data, Theme, and Addressables assets are immutable inputs. Any mutation, targeted save/import, or post-save validation failure restores the entire managed set and verifies the restored hashes. A successful Apply must leave the complete Addressables file inventory and bytes unchanged and preserve the GUID and local file IDs of all 11 Theme font assets, their materials, and atlases. The Python validator is a faster outer check, not a bypassable source of authority.
