# Achievement and participant reset integration

Source: reset/restart `533820477` plus efficient achievements `1b59597be`.
Worktree: `/mnt/d/J2M/worktrees/ar18`, branch `integration/achievements-reset-v2`.
Evidence: `/mnt/d/J2M/evidence/achievements-reset-v2`.

## Intent and compatibility

- Production has 18 achievements; participant reset uses all 18 exact mapped Steam names.
- Reset mapping is `level-and-efficient-clear-v2`; journal schema remains 1.
- Previous and unknown mapping versions are rejected for **both Pending and Ready**.
- No compatibility, migration, ignoring, automatic deletion, or overwrite of incompatible records.
- Normal startup from incompatible records holds publication/campaign access and explains that test data cleanup is needed.
- Steam Clear failure, schema failure, or readback failure is not reset success.
- The original two source worktrees and their user changes are preserved.
- Existing menu Prefab/localization/font changes come from the reset branch and expose its participant reset UI. This patch introduces no new Scene/Prefab/font edits.

## Tests-first evidence

- Initial Unity attempt: test compilation failed due to a missing test namespace import; corrected before behavioral evidence.
- RED rerun: 177 EditMode tests, 156 passed / 21 failed / 0 skipped. Failures expose accepted incompatible records, old target version, and rejected current helper records. See `red-r2/failures.json`.
- Cold PowerShell RED: exact helper sources compiled; current-v2 Ready was rejected by the old wire validator. See `helper-red.log`.
- Production changes were applied only after these results.

## Final validation

- Focused GREEN: EditMode 177 passed, 0 failed; PlayMode 0 matching tests.
- Integrated filtered full: EditMode 795 passed, 0 failed, 1 graphic menu-preview skipped; PlayMode 0 passed, 0 failed, 1 graphic pointer/menu test skipped. Not an unfiltered full result.
- UI lane completed successfully; detailed XML and Windows build output are under `ui/` in the evidence root.
- Windows helper harness: 81 passed, 0 failed, including cold compilation of copied helper sources and v2/legacy wire checks; no Steam/game/native API execution.
- Distribution staging wrapper: initial 6/9 passed because its inherited synthetic fixture omitted newly required helpers and reset DLLs. The fixture now includes those files and checks staged bytes; final 9/9 passed.
- Release wrapper: all 164 cases passed before commit, but immutable evidence export correctly rejected the dirty merge worktree. The complete command is rerun after commit to produce committed-source evidence.
- Core validation is enforced by the normal commit hook; the commit log and `commit-core/` evidence record that execution.
- Independent review found no remaining old Ready bypass, startup publication bypass, or incompatible-record overwrite path.

No build upload or real Steam account reset is part of this change.
The existing Win32 access-denied issue in actual Steam restart is not claimed fixed.
