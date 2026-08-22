# Campaign Save Rollback Retention Policy

Status: historical, superseded

This filename is retained so historical links remain valid. Its former
two-public-release rollback and legacy PlayerPrefs retention policy is
superseded by
[Pre-Release-Save-Baseline-Policy.md](./Pre-Release-Save-Baseline-Policy.md).

VectorQuake has no public prior product version or save schema. Production does
not provide a PlayerPrefs progression rollback composition, missing-profile
import, migration marker, source-hash policy, or deleted-slot resurrection
guard.

Historical reports and Git history may describe the removed design. They are
private provenance only and must not be interpreted as current runtime,
retention, cleanup, or release policy.

Current truth:

- campaign progression: `Saves/profile.json`
- committed local active state: `Saves/local-launch-state.json`
- missing canonical profile: valid current-schema backup recovery 후에만 normal no-save state
- PlayerPrefs progression compatibility: unsupported
- audio/display/input/locale PlayerPrefs settings: preserved
- broad registry deletion: forbidden
