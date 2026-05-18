# Repository Instructions

## Project
- This is a Unity/C# game project.
- Run validation lanes from WSL with `./run_tests.sh`.
- Keep repo-level rules short here; route architecture details to `Docs/Architecture/README.md`.

## Read First
- Before editing, read:
  - `Docs/Architecture/README.md`
  - `Docs/Testing/Gameplay-Test-Automation-Guide.md`
  - `AI_GIT_COMMIT_RULES.md`
- Start every change by checking:
  - `git status --short --branch`
  - `git diff --stat`

## Before Editing
- Do not revert user changes unless explicitly requested.
- Understand the current diff before staging or modifying files.
- Keep changes scoped to the requested intent.

## Change Boundaries
- Prefer one intent per commit.
- Keep Scene changes in a separate commit when possible.
- State the purpose of Prefab and ScriptableObject changes.
- Keep asset changes paired with their `.meta` files when Unity generates them.

## Architecture Guardrails
- `WorldState` is the authoritative mutable gameplay state owner; `WorldSnapshot` is the read-only query seam.
- Tick flow stays `GameplaySceneHost -> TickRunner -> TickPipeline -> TickResult -> Presenter`.
- Do not mix semantic phases (`Movement`, `Attack`, `Cleanup`, `Respawn`) with execution stages (`Plan`, `Resolve`, `Finalize`, `Cleanup`, `Respawn`).
- Authoritative writes should flow through `Finalize`, batch apply, or committer paths unless a documented exception applies.
- Presentation, UI, audio, and topology visuals must not mutate authoritative simulation.
- Tiles are canonical `SurfaceCell(face, x, y)`, not plain `(x, y)`.
- Occupancy lanes are gameplay lanes such as `Unit`, `Solid`, and `Projectile`, not Unity rendering layers.
- Do not collapse traversal geometry, placement legality, and settlement legality into one generic boolean.
- Box behavior is `EntityType.Box` plus `BoxCapabilities`; Push/Flip are player actions, not ordinary movement.
- Enemy AI follows the profile compile path: `EnemyAiProfile -> Compiler -> RuntimeDefinition -> LogicProvider -> TickPipeline`.
- UI follows runtime composition: `Gameplay UIAccess -> UI.Application -> UI.Flow -> ViewModel -> View`.
- Audio is runtime presentation concern; `WorldState`, `TickPipeline`, and entity logic must not play audio directly.
- Stage runtime roots come from `StageCatalog -> StageContentEntry -> companion definitions`.
- Topology transition visuals consume presentation carriers such as `TickPresentationData.TopologyMotion`; visual bridge, post-fx, and camera shake are presentation-only.

## Testing
- Default validation: `./run_tests.sh core`.
- Test lanes must validate the current worktree; do not run Unity with a hardcoded main worktree `-projectPath`.
- Use `./run_tests.sh` from the worktree being validated; do not call Unity directly for repository lane evidence.
- UI changes require `./run_tests.sh ui` or an explicit not-run reason.
- Gameplay/core changes require `./run_tests.sh core` and, when risk warrants it, touched-cluster targeted tests.
- Scene, Prefab, ScriptableObject, and asset changes require purpose plus manual/editor validation evidence when applicable.
- The documented full lane baseline is red; do not treat unrelated baseline failures as touched-cluster regressions.

## Commit and PR
- Follow `AI_GIT_COMMIT_RULES.md` for commit split and message format.
- Commit messages use `<type>: <scope> - <summary>` plus a body with meaningful bullets.
- PR evidence must list tests run, tests not run, and reasons.

## Forbidden Claims
- Do not claim `project-wide green`, `full regression closed`, `all regressions fixed`, or `full lane green` unless the matching full/broad lane actually ran and passed on the same revision.
- When full is red or not run, report touched-cluster results separately from unrelated baseline failures.
