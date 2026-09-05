# KBO Dia Gothic Typography Migration Closeout

## Scope and decision

This document is the current source of truth for governed Korean UI fonts. It
supersedes the Climate Crisis KR 2000/2019 runtime mapping while preserving the
existing 19 semantic roles and authored sizing policy.

- KBO Dia Gothic Medium is the large-text face. It resolves the 10 existing
  Display, UI, and Utility role entries used by large or emphasis-heavy
  text.
- KBO Dia Gothic Light resolves the remaining 9 Heading and Body roles used by
  smaller headings, body copy, tooltips, and popup copy.
- The theme remains `PRESERVE_AUTHORED_SIZE`: locale rules do not own font size,
  Auto Size, min/max range, line spacing, or character spacing.
- Every `ko-KR` role uses the real Light or Medium face with TMP Normal style;
  synthetic bold is not used.
- The `en-US` serialized theme contract is unchanged.

The exact role split remains:

| KBO face | Semantic roles |
|---|---|
| Medium | `HeaderLarge`, `Button`, `Label`, `Value`, `Status`, `SettingsDisplay`, `SettingsBody`, `SettingsAction`, `SettingsStatus`, `MainMenuCommand` |
| Light | `Default`, `HeaderMedium`, `HeaderSmall`, `Body`, `BodySmall`, `Tooltip`, `SettingsLabel`, `PopupBody`, `PopupAction` |

## Font provenance and general permission

KBO Dia Gothic is published by the Korea Baseball Organization (KBO). The
official distribution page describes Light as the body face and Medium as the
body-title face. The current page and License Guide Ver.2 permit free individual
and corporate use, commercial use, redistribution of the original font, and
font-file embedding in web, mobile, desktop, and server software. The table also
expressly includes mobile-game software, application/system fonts, and
game-device UI fonts. On that basis, normal UI and body-text use in a paid PC
game is within the published embedding scope; this is an application of the
listed categories, not a platform-specific Steam approval.

- [KBO official font and license page](https://www.koreabaseball.com/Reference/etc/KboFont.aspx)
- [KBO Dia Gothic License Guide Ver.2 (PDF)](https://6ptotvmi5753.edge.naverncp.com/KBO_FILE/file_down/KBO_%EB%8B%A4%EC%9D%B4%EC%95%84%EA%B3%A0%EB%94%95_%EB%9D%BC%EC%9D%B4%EC%84%A0%EC%8A%A4_%EC%95%88%EB%82%B4_Ver2.pdf)

The repository keeps the supplied Light and Medium TTF bytes unmodified.
`Assets/_Shared/UI/Fonts/KBO-Dia-Gothic-LICENSE.txt` records the reviewed source,
terms, and contact. No explicit end-credit or license-file bundling requirement
was identified in the reviewed official materials; the repository notice is a
voluntary provenance and release-governance record rather than a claimed
license condition.

## License boundaries and release review item

The published CI/BI prohibition is broader than a prohibition on drawing a
logo. KBO Dia Gothic must not be used as representative typography for a
company name, brand name, product name, logo, mark, slogan, catchphrase, or
brand package design. Accordingly, use it for ordinary commands, settings,
scores, dialogue, and explanatory copy, but not for representative
`VectorQuake` game-title, `J2M` developer-identity, store-capsule, game-icon, or
brand-slogan treatments. An incidental textual mention is not automatically a
brand treatment; classify the purpose and prominence of the presentation. The
current main-menu brand logo is a separate `UnityEngine.UI.Image` backed by
`Assets/3DM/Sprite/tittl3e.png`, not a governed KBO TMP text role, and its own
artwork/font provenance remains a separate review item.

The font itself may not be sold. Preserve the supplied distribution form: do
not edit the source TTFs or redistribute a modified or adapted font. The license
also prohibits use on illegal sites, in false or exaggerated advertising, and
in works contrary to public order or accepted morality. KBO may use images of
printed materials and advertising materials, including online advertising,
made with the font for KBO promotion, with an opt-out request available to the
user.

Unity technically generates TMP font assets, SDF atlas textures, materials, and
character data from a source TTF without requiring the source TTF bytes to be
rewritten. That technical distinction supports treating the generated assets as
application rendering data, but it is not a KBO rights-holder determination.
The published KBO materials do not expressly address TextMesh Pro SDF assets or
confirm how distribution of those generated assets is treated under the
modification/adaptation restriction. Static versus Dynamic atlas population
changes source-font linkage and runtime behavior, but does not settle this
license classification.

Before release, seek and record a written answer from
`kbop@koreabaseball.or.kr`. Until an answer is received, do not describe
distributed TMP/SDF assets as explicitly approved. This is a documented release
review item, not a claim that the published license expressly prohibits SDF
generation and not an automated build-approval gate. The release owner must
record the reply or the decision taken with the remaining uncertainty. The
focused inquiry is:

> 원본 TTF의 내용과 글자 디자인은 변경하지 않고, Unity TextMeshPro로 필요한 글자의 SDF 아틀라스와 표시용 데이터를 생성하여 유료 게임에 포함하는 방식이 허용되는 임베딩에 해당하는지 확인 부탁드립니다.

Until that answer is received, the project status is: general commercial UI use
and original-font embedding reviewed as permitted; representative CI/BI use is
excluded; TMP/SDF distribution remains a documented rights-holder-confirmation
item.

## Canonical asset identity

Existing TTF and SDF GUIDs and the SDF material/atlas local IDs are preserved so
that the migration does not rewrite Prefab, Scene, or ScriptableObject
references.

| Identity | KBO Medium | KBO Light |
|---|---|---|
| Source TTF | `KBODiaGothic-Medium.ttf` | `KBODiaGothic-Light.ttf` |
| TTF GUID | `5360535d0de75234ca21822297323672` | `56e1f07e315e49a4a8e5043a11e04e29` |
| TTF SHA-256 | `f88f06494fc4eb8fd06e15c1f6deacfa8d7855c9a4245d71962a90596ad41f02` | `607c0a894ea951489bd43f6a3ccc93adececbb46c425ccc5869f2327dbcfe747` |
| TMP SDF | `KBODiaGothic-Medium SDF.asset` | `KBODiaGothic-Light SDF.asset` |
| SDF GUID | `40d61154fd6576b4d85c2d78460b16ad` | `7dfd9aae81fc1d242b007a3b7a042fb0` |
| SDF SHA-256 | `700a62c77f523814a5fd8680018a1803322ff175e47abc57f26df202e3ee442e` | `b0f27ca1b7dca01498c1613fc0c39b1264e37d68fdf37cd6e7559323a0c8ef90` |
| Material local ID | `1352911973252649374` | `7808543287137721147` |
| Atlas local ID | `-2536001923755311345` | `-5757234995057936259` |

Both SDF assets are static, single-atlas assets with an empty fallback table.
Their face metadata resolves to family `KBO Dia Gothic` and style `Medium` or
`Light`. Managed `*_ko-KR.asset` String Tables must have zero missing native
glyphs in both assets.

## Generation and validation contract

`./run_tests.sh kbo-glyph-update` is the only repository command for rebuilding
the managed glyph corpus. It regenerates both atlases from the source TTFs,
preserves the tracked GUID/material/atlas identity, verifies source linkage,
native managed glyph coverage, fallback absence, and single-atlas continuity,
and atomically restores both SDF files on failure.

UI changes require `./run_tests.sh ui`. The typography contract tests cover:

- the Medium/Light source, face metadata, GUID, material, and atlas identity;
- the 10/9 role mapping and all 19 Korean resolutions;
- unchanged English theme serialization and authored sizing;
- native managed Korean glyph coverage with no fallback;
- the existing Pause title, Settings audio-value, and Display-status layout
  contracts.

Visual review remains required because a font replacement changes glyph shape,
advance, wrapping, and perceived weight even when all structural tests pass.
`./run_tests.sh typography-visual` is the canonical Settings/Pause/Main Menu
capture lane; other governed HUD/result visual lanes should be run when release
evidence is assembled.

User-performed manual visual validation was completed on 2026-09-05 KST. The
automated `./run_tests.sh typography-visual` capture lane was not run for this
working-tree migration, so no automated screenshot bundle is claimed here.

## Historical boundary

Climate Crisis KR and Nanum references in older audit and visual-evidence
documents describe earlier revisions only. They are not current runtime asset
requirements. No global TMP fallback, package setting, semantic-role expansion,
or authored-size retuning is part of this migration.
