---
name: j2m-localization-atlas
description: Maintain J2M's governed en-US, ko-KR, ja-JP, and zh-CN UI localization and rebuild exact-corpus Static TMP font atlases. Use when localization keys or translations change, when a locale is admitted, or when typography atlas coverage is updated; do not use for ordinary non-localized UI copy edits.
---

# J2M localization and atlas workflow

Read [references/contract.md](references/contract.md) before changing localization data or font assets.

1. Inspect the current diff and preserve unrelated user work. Treat the committed English UI and Stage String Tables as the source-key inventory.
2. Run the localization draft validation lane. If English keys or source text changed, update `Docs/Localization/Four-Locale-Translation-Draft.csv` so it has one exact row per governed key.
3. Produce Japanese and Simplified Chinese Draft text in two passes: first a faithful translation, then a concise game-UI edit. Preserve Smart String placeholders, TMP tags, escaped newlines, identifiers, and numbers exactly.
4. Keep every changed row in `Draft`. Show the user the changed translations and request explicit human approval. Do not register a Draft locale, write Draft text into production String Tables, or generate shipping atlases from it.
5. After approval, change only the approved rows to `Approved`, run the approved-apply command, and review the resulting locale, String Table, Theme, font, material, atlas, Addressables, and `.meta` diffs together.
6. Run the UI validation lane. Report targeted results separately from unrelated baseline failures.

Fail when a governed key, source match, translation, placeholder, font hash, glyph, locale table, Theme mapping, Static atlas contract, or residency contract is missing. Runtime missing text uses the visible `□` sentinel; it must never silently fall back to English.
