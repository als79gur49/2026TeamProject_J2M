# Achievement and participant reset integration

Source: reset/restart `533820477` plus efficient achievements `1b59597be`.
Worktree: `/mnt/d/J2M/worktrees/ar18`, branch `fix/participant-reset-legacy-recovery`.
Prior integration evidence: `/mnt/d/J2M/evidence/achievements-reset-v2`.

## Intent and compatibility

- Production has 18 achievements; participant reset uses all 18 exact mapped Steam names.
- Reset mapping is `level-and-efficient-clear-v2`; journal schema remains 1.
- Known previous `level-clear-v1` Ready is archived unchanged on ordinary startup, then normal services resume without Steam reset, local progress deletion, or restart. The archive preserves the save-seed suppression history on future starts.
- Known previous Pending holds ordinary services until the user explicitly confirms a new reset. Matching the recorded Steam account/AppID is required; confirmation creates a new v2 operation GUID and atomically replaces the active journal, retaining the previous bytes in the archive.
- Previous Pending is not silently resumed with the expanded 18-target mapping. No efficient-clear history reconciliation or retired-ID conversion is introduced.
- Unknown mappings and corrupt records remain blocked and unchanged. The completed-return helper accepts only v2 Ready; old completed-return requests still fail closed.
- Steam Clear failure, schema failure, or readback failure is not reset success.
- The original two source worktrees and their user changes are preserved.
- Existing menu Prefab/localization/font changes come from the reset branch and expose its participant reset UI. This patch introduces no new Scene/Prefab/font edits.

## Prior strict-policy tests-first evidence

- Initial Unity attempt: test compilation failed due to a missing test namespace import; corrected before behavioral evidence.
- RED rerun: 177 EditMode tests, 156 passed / 21 failed / 0 skipped. Failures expose accepted incompatible records, old target version, and rejected current helper records. See `red-r2/failures.json`.
- Cold PowerShell RED: exact helper sources compiled; current-v2 Ready was rejected by the old wire validator. See `helper-red.log`.
- Production changes were applied only after these results.

## Prior strict-policy validation

- Focused GREEN: EditMode 177 passed, 0 failed; PlayMode 0 matching tests.
- Integrated filtered full: EditMode 795 passed, 0 failed, 1 graphic menu-preview skipped; PlayMode 0 passed, 0 failed, 1 graphic pointer/menu test skipped. Not an unfiltered full result.
- UI lane completed successfully; detailed XML and Windows build output are under `ui/` in the evidence root.
- Windows helper harness: 81 passed, 0 failed, including cold compilation of copied helper sources and v2/legacy wire checks; no Steam/game/native API execution.
- Distribution staging wrapper: initial 6/9 passed because its inherited synthetic fixture omitted newly required helpers and reset DLLs. The fixture now includes those files and checks staged bytes; final 9/9 passed.
- Release wrapper: all 164 cases passed before commit, but immutable evidence export correctly rejected the dirty merge worktree. The complete command is rerun after commit to produce committed-source evidence.
- Core validation is enforced by the normal commit hook; the commit log and `commit-core/` evidence record that execution.
- That earlier review enforced blanket old-record rejection. The recovery policy above supersedes that behavior and requires fresh validation below.

No build upload or real Steam account reset is part of this change.
The existing Win32 access-denied issue in actual Steam restart is not claimed fixed.

## Legacy recovery validation scope

The earlier counts above belong to the previous strict-rejection revision. They do not
validate this recovery change. Current tests cover the real file journal with fake reset
and publication dependencies: old Ready archival before ordinary startup, exact archived
bytes and no reset calls, old Pending preservation and blocked services before explicit
confirmation, plus unknown/corrupt records remaining unchanged and publication deferred.
Coordinator/service tests cover the explicit request, new operation identity, account
mismatch, atomic replacement, and failure handling. The Windows helper keeps the existing
cold-compilation and old-completed-return rejection checks.

Current recovery evidence is stored under `/mnt/d/J2M/evidence/participant-reset-legacy-recovery`.

- The first focused invocation used an invalid `|` separator and selected zero tests;
  its nonzero runner result is not behavioral validation. The corrected filter uses `;`.
- Corrected focused EditMode: 241 passed, 0 failed, including actual file-lock failures
  after archival, retry without lost requests, account mismatch, startup and localization.
- Cold Windows helper harness: 81 passed; fake inputs only, with no Steam/game/native calls.
- Graphic PlayMode menu flow: 3 passed, 0 failed, 0 skipped (confirmation, cancellation,
  blocked restart and pointer routing). Test dependencies are fake; no participant data reset.
- Initial UI lane: 1359 passed / 1 failed because the managed string count omitted three new entries.
  After correcting that inventory, glyph validation exposed five unsupported Korean glyphs.
  The final Korean copy uses the existing native glyph set; no font assets were modified.
- Final UI lane (`ui-r3`): Windows UI build and 1360 EditMode tests passed, including
  native Korean glyph validation for both KBO fonts.
- Core lane (`core-commit`, normal commit hook): Windows core build, EditMode 254 passed;
  PlayMode 107 passed / 4 graphic-only skips / 0 failed. The final UI commit also uses
  the same hook; its separate evidence is under `ui-commit`.
- No unfiltered full lane or real Steam account reset was run. No Steam upload is included.

Actual Steam access-denied recovery, forced process termination/power loss, and participant
re-acquisition remain separate live validation requirements. Archive writes flush file data
before rename/replace; the tests do not certify filesystem durability through hardware failure.
