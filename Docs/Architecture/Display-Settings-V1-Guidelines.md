# Display Settings V1 Guidelines

This document is the display-settings-specific supplement to the canonical UI architecture rules in [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md).

## Boot Timing
- `DisplayRuntimeInstaller` is the sole shared bootstrap owner for persistent display settings.
- It must live on the same canonical bootstrap root as `GameplayUiFlowInstaller`.
- It applies saved committed settings in `Awake()` through `Install()`.
- `Install()` is idempotent and boot apply runs once only.
- Boot normalization must never save.
- Boot apply must skip `Screen.SetResolution` when the normalized target already matches live runtime state.

## Catalog And Normalization Policy
- V1 sources visible display modes from runtime-supported system resolutions only.
- Visible ordering preserves first-seen `width x height` order from the runtime catalog.
- Duplicate entries collapse by `width x height`.
- Duplicate entries keep visible order but upgrade the internal refresh choice to the highest refresh observed for that visible size.
- V1 visible labels remain `width x height` only.
- Refresh remains internal-only in v1. It is selected by the runtime service and persisted with committed settings, but it is not exposed as a dropdown.
- Unsupported runtime fullscreen modes normalize to visible `FullScreenWindow`.
- Saved `width x height` values that no longer exist fall back to the normalized current runtime resolution when possible, otherwise the first visible catalog entry.
- Normalization is read/apply-only until an explicit user commit.

## Preview Lifecycle Ownership
- `DisplaySettingsService` owns current committed state, preview-applied state, preview revert, and persistence.
- `SettingsScreenPresenter` owns staged UI state only.
- `DisplayPreviewSessionHost` owns the confirm popup instance, single active preview session, preview timeout seconds, timeout arm/cancel, and single completion routing.
- `DisplayPreviewTimeoutRelay` owns preview countdown tick tracking on the canonical root using real runtime time.
- `DisplaySettingsLifecycleRelay` is the safe resync trigger for focus regain and pause return.
- Cancel, popup close, timeout, screen transition, `SetIsCurrent(false)`, and dispose all route to one idempotent preview-cancel path.

## Same-Root Repair Note
- The required scene repair is simple: add exactly one `DisplayRuntimeInstaller` to the same bootstrap root `GameObject` that already owns `GameplayUiFlowInstaller`.
- Do not add a scene-global fallback lookup.
- Do not place the installer on another root and rely on discovery.
- Do not duplicate the installer on the canonical root or child objects.

## V1 Scope Note
- V1 supports only `Windowed` and `FullScreenWindow`.
- V1 does not add `ExclusiveFullScreen`, render scale, dynamic resolution, graphics presets, or refresh-rate UI.
- Future refresh-rate UI must treat the current v1 behavior as “resolution labels stay visible-only while refresh remains internal and persisted.” Do not reinterpret v1 as if refresh never existed.

## Settings Resolution Hover Hint Policy
- The Settings resolution hover hint is a local SettingsDisplaySection affordance, remains available regardless of the Tooltips accessibility toggle, and does not use TooltipPopup or popup flow.
- It is pointer-hover-only in v1 and must not be treated as precedent for popup tooltip auto-hide or global hover infrastructure.

## Settings Preview Countdown Policy
- The Settings display preview countdown is a local `SettingsDisplaySection` indicator whose timeout value comes from `DisplayPreviewSessionHost`, while confirm popup copy remains static text derived from that same timeout source.
- Countdown visibility starts only after confirm popup open succeeds, v1 rendering is whole-second stepwise text plus bar from the same snapshot ticks, and v1 does not add live popup countdown UI.

## Settings Authored Child-View Checklist
- `SettingsScreen.prefab` must author exactly one root shell with child section roots named `SettingsAudioSection` and `SettingsDisplaySection`.
- `SettingsAudioSection` must carry `SettingsAudioView`, and `SettingsDisplaySection` must carry `SettingsDisplayView`.
- `SettingsScreenView` must serialize `_audioView` and `_displayView` directly to those child views; runtime binding must not rely on hierarchy search to rediscover them.
- `SettingsAudioView` must serialize complete authored row references for `Main`, `Bgm`, and `Sfx`: row root, label, value label, slider, mute toggle, and slider interaction relay.
- `SettingsDisplayView` must serialize complete authored references for `_sectionTitle`, `_currentDisplayLabel`, `_currentDisplayValue`, `_resolutionLabel`, `_resolutionDropdown`, `_resolutionInfoHotspot`, `_resolutionHoverRelay`, `_resolutionHoverHintRoot`, `_resolutionHoverHintLabel`, `_fullscreenLabel`, `_fullscreenToggle`, `_displayStatusLabel`, `_previewCountdownRoot`, `_previewCountdownLabel`, `_previewCountdownFill`, `_applyButton`, `_applyButtonLabel`, `_revertButton`, and `_revertButtonLabel`.
- Authored audio controls must remain descendants of `SettingsAudioSection`, and authored display controls must remain descendants of `SettingsDisplaySection`.
- Root-shell compatibility helpers remain passthrough-only. They do not own section-local widgets, section-local state, preview policy, or display/audio business logic.
- Settings authored child-view canonicalization is complete. Future prefab authoring changes should edit canonical prefab assets directly and rely on focused runtime and behavior tests.
