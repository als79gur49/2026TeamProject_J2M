---
name: pr-workflow
description: Use when inspecting, creating, updating, or responding to review on pull requests in this Unity/C# repo; checks PR identity, diff, CI, reviews, conflicts, touched clusters, and validation evidence.
---

# PR Workflow

## When to Use
- Use this skill for pull request investigation, PR creation, PR description updates, CI triage, review response, and pre-merge evidence checks.
- If a PR task requires commits or pushes, follow `commit-push-workflow`.

## Start
- Run `git status --short --branch`.
- Run `git diff --stat`.
- Protect local user changes before fetching, switching branches, or staging.

## Identify PR
- Confirm PR number, repository, base branch, and head branch.
- Confirm whether the PR is draft or ready for review.
- Confirm the latest head SHA before using CI or review evidence.

## Fetch and Local Safety
- Fetch remote refs before comparing PR branches.
- Do not overwrite local work.
- Do not merge, rebase, cherry-pick, or force push unless explicitly requested.

## Inspect Diff
- Inspect changed files and the PR diff.
- Classify touched clusters:
  - core/gameplay
  - UI
  - audio
  - stage/content
  - Scene
  - Prefab
  - ScriptableObject
  - asset/`.meta`
- Check that Scene, Prefab, ScriptableObject, and asset changes have a stated purpose.

## Check CI
- Check CI status for the PR head SHA.
- Inspect failing job names and relevant logs.
- Separate infrastructure or unrelated baseline failures from touched-cluster failures.

## Check Review Comments
- Check review comments and unresolved threads.
- Address only actionable feedback unless the user asks for a broader rewrite.
- Report which comments were fixed, deferred, or require a product decision.

## Conflict Risk
- Check whether the PR can merge cleanly into the base branch.
- Do not resolve conflicts by merge, rebase, cherry-pick, or force push without explicit instruction.

## Touched Cluster
- Map changed files to validation evidence.
- Gameplay/core changes require `./run_tests.sh core` or a not-run reason.
- UI changes require `./run_tests.sh ui` or a not-run reason.
- Stage/content changes may require catalog validation or related editor tests.
- Scene, Prefab, ScriptableObject, and asset changes require manual/editor validation evidence when automation is not available.

## Validation Evidence
- Record tests that ran.
- Record tests not run and the reason.
- Do not combine evidence from different revisions into one claim.
- Treat test evidence as valid only when it comes from the PR/head worktree.
- Do not reuse stale `TestResults/` from another worktree.
- Include the `PROJECT_PATH_WSL` / `PROJECT_PATH_WIN` summary when path ambiguity matters.
- Separate path/config dry-run success from test lane success.
- If full baseline is red, report touched-cluster results separately from unrelated baseline failures.

## PR Description
- Include what changed and why.
- Include validation evidence with command names and outcomes.
- Include skipped tests with reasons.
- Include manual/editor evidence for Scene, Prefab, ScriptableObject, stage, and asset changes when applicable.

## Forbidden Claims
- Do not claim `project-wide green`.
- Do not claim `full regression closed`.
- Do not claim `all regressions fixed`.
- Do not claim `full lane green` unless full actually ran and passed on the same revision.
- Do not imply broad project health from `core`, `ui`, or targeted checks alone.

## Safety
- Do not merge, rebase, cherry-pick, close, reopen, or force push without explicit instruction.
- Do not dismiss reviews or resolve review threads unless the requested fix was made and the resolution is clear.
- Do not edit `.git/hooks` as part of PR work unless explicitly requested.

## Report
- Report PR identity, branch state, CI state, review state, conflict risk, touched clusters, and validation evidence.
- Call out unresolved risks and skipped checks directly.
