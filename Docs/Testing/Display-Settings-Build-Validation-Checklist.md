# Display Settings Build Validation Checklist

Use this checklist only for real build validation.

Approved reporting levels:
- `build verified`
- `core lane validated`
- `targeted display architecture validated`
- `real-build manual display validation completed`

Editor-only execution is insufficient evidence for fullscreen/window correctness.

## Required Build Checks
- startup apply:
  - verify the last committed display settings are applied on boot
  - verify there is no visible startup resolution flash or fullscreen bounce
  - verify UI layout is stable immediately after boot apply
- preview confirm:
  - open `SettingsScreen`
  - change resolution and/or fullscreen
  - press `Apply`
  - confirm the preview popup
  - verify the new display settings remain active after confirmation
- timeout revert:
  - start a display preview
  - do not confirm
  - verify automatic revert after 15 seconds
- cancel and close revert:
  - cancel from the preview popup
  - back out of the popup
  - leave the screen while preview is active
  - verify each path reverts once and leaves no stale popup
- focus and windowing behavior:
  - alt-tab during preview and after commit
  - minimize and restore
  - verify the runtime label resync is sane and saved state is not silently changed
- environment-sensitive checks:
  - multi-monitor behavior if relevant for the target setup
  - CanvasScaler / anchor stability after resolution changes
