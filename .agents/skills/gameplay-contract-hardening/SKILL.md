---
name: gameplay-contract-hardening
description: Use for high-risk gameplay contract work involving runtime policy drift, enemy/runtime contracts, TileFeature/SurfaceCell rules, traversal/placement/settlement legality, occupancy/blocker vocabulary, or gameplay touched-cluster failures.
---

# Gameplay Contract Hardening

## When to Use
- Use for gameplay policy drift, runtime contract tests, enemy runtime contracts, TileFeature policy, SurfaceCell face identity, traversal/placement/settlement legality, occupancy lane regressions, blocker vocabulary drift, and gameplay contract test failures.
- Use for test-failure triage only when failures concern gameplay contracts or files touched in a gameplay/core cluster.

## When Not to Use
- Do not use for generic test failures unrelated to gameplay contracts or touched gameplay/core files.
- Do not use for commit, push, or PR workflow; use `commit-push-workflow` or `pr-workflow`.
- Do not use for pure UI, audio, VFX, BGM, or presentation-only work unless the root issue is a gameplay contract boundary.
- Do not use for broad architecture investigation without a concrete gameplay contract risk.

## Start
- Run `git status --short --branch`.
- Run `git diff --stat`.
- Read `AGENTS.md`.
- Before edits, read:
  - `Docs/Architecture/README.md`
  - `Docs/Testing/Gameplay-Test-Automation-Guide.md`
  - `AI_GIT_COMMIT_RULES.md`
- If the task reaches commit/push work, switch to `commit-push-workflow`.
- Treat existing dirty files as user-owned unless explicitly assigned to the task.

## Classify the Contract
- Classify the behavior before editing:
  - `StrongContract`: fixed by canonical docs, architecture guardrails, tests, public API shape, determinism, or authoritative write path.
  - `CurrentPolicy`: current behavior that exists in code but is not clearly frozen as a contract.
- If the classification is unclear, stop and report the missing evidence before changing tests or production code.
- Do not rewrite tests to match an assumption until this classification is explicit.

## Audit Runtime Paths
- Compare helper/precheck paths with actual resolve/finalize paths.
- Check both policy helpers and runtime execution seams before changing production code.
- Keep authoritative writes inside approved `WorldState`, `Finalize`, batch apply, or committer paths.
- Do not collapse `SurfaceCell(face, x, y)` into plain `(x, y)`.
- Do not merge traversal geometry, placement legality, and settlement legality into one generic boolean.
- Preserve occupancy lane vocabulary such as `Unit`, `Solid`, and `Projectile`.

## Tests First
- When policy drift is suspected, add or correct focused contract tests before production fixes when practical.
- Prefer targeted tests around the touched gameplay cluster.
- Keep test names and assertions tied to contract vocabulary, not incidental implementation details.

## Gameplay Validation
- For gameplay/core code or test changes, run targeted touched-cluster tests and `./run_tests.sh core`.
- If Unity lanes are not run, state exactly why.
- Separate touched-cluster failures from unrelated baseline failures.
- Do not claim project-wide green, full regression closure, or broad gameplay-wide validation unless the matching broad lane ran and passed on the same revision.

## Report
- Report contract classification, touched runtime paths, tests changed or added, validation commands, skipped checks, and remaining risks.
