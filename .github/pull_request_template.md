# Summary

## What changed
-

## Why
-

# Change Type

- [ ] Core/gameplay
- [ ] UI
- [ ] Audio
- [ ] Stage/content
- [ ] Scene
- [ ] Prefab
- [ ] ScriptableObject
- [ ] Asset/.meta
- [ ] Docs/tooling only

# Tests

- [ ] `./run_tests.sh core`
- [ ] `./run_tests.sh ui`
- [ ] Targeted test:
- [ ] Test worktree confirmed: current PR/head worktree
- [ ] Not run, reason:

# Evidence

- UI changes include `./run_tests.sh ui` evidence or a skipped reason.
- Gameplay/core changes include `./run_tests.sh core` evidence or a skipped reason.
- Scene, Prefab, and ScriptableObject changes state their purpose and manual/editor validation.
- Asset changes include a `.meta` pairing check.
- Stage content changes include catalog validation or related editor tests when applicable.

# Baseline Red Handling

- Touched cluster result:
- Unrelated baseline failure:

# Forbidden Claims Unless Proven

- Do not claim `project-wide green`.
- Do not claim `full regression closed`.
- Do not claim `all regressions fixed`.
- Do not claim `full lane green` unless `./run_tests.sh full` actually ran and passed on this revision.
