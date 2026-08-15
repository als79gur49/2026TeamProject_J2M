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
  - in borderless fullscreen, verify the visible cursor remains inside the focused Player window
  - move the custom cursor to all four edges and verify its pointer tip remains visible without complete disappearance
  - after alt-tab or minimize, verify cursor confinement releases so other applications and displays remain usable
  - after returning focus to borderless fullscreen, verify cursor confinement is restored
  - in windowed mode, verify the cursor can leave the Player window
  - switch both directions with the native fullscreen shortcut and verify confinement follows the live window mode
- environment-sensitive checks:
  - on a multi-monitor setup, verify the cursor cannot cross to an adjacent display while the fullscreen Player is focused
  - verify the adjacent display becomes usable after the Player loses focus
  - CanvasScaler / anchor stability after resolution changes
