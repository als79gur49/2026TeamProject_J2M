---
name: commit-push-workflow
description: Use when preparing, splitting, committing, or pushing local Git changes in this Unity/C# repo; enforces staged diff review, user-change protection, commit message rules, and push verification.
---

# Commit Push Workflow

## When to Use
- Use this skill for any request to prepare commits, split commits, write commit messages, or push branches.
- This is a Codex workflow, not a Git hook.
- The versioned `commit-msg` hook validates message format; this skill governs the full commit and push process.

## Start
- Run `git status --short --branch`.
- Run `git diff --stat`.
- Read `AI_GIT_COMMIT_RULES.md` before writing commit messages.
- If there are existing user changes, identify which files belong to the current task before editing or staging.

## Inspect
- Use `git diff` for unstaged changes that may be committed.
- Use `git diff --stat` to understand breadth.
- Do not stage changes before understanding the diff.

## Protect User Changes
- Never revert user changes unless explicitly requested.
- Do not use destructive commands without explicit instruction:
  - `git reset --hard`
  - `git checkout -- <path>`
  - `git clean`
  - force push
- If task changes overlap with user changes, work with the current file content instead of discarding it.

## Plan Commit Split
- One commit should carry one intent.
- Split structure-only changes from behavior changes.
- Split UI changes from gameplay/core changes when practical.
- Keep Scene changes separate when possible.
- Consider separate commits for Prefab, ScriptableObject, data, asset, and `.meta` changes when the purpose differs.

## Stage
- Stage only files that belong to the chosen commit intent.
- Prefer explicit paths or `git add -p` when the worktree contains unrelated changes.
- Before committing, inspect staged content with `git diff --staged`.
- Confirm staged Scene, Prefab, ScriptableObject, asset, and `.meta` files match the commit purpose.

## Commit Message
- Use this title format:
  - `<type>: <scope> - <summary>`
- Allowed types:
  - `feat`
  - `fix`
  - `refactor`
  - `chore`
  - `docs`
  - `test`
- The body is required.
- Leave one blank line between title and body.
- Use at least two body bullets.
- Body bullets must explain why the change was made and what changed as a result.
- Do not use vague summaries such as `update`, `수정`, `작업`, `변경`, or `변경사항 반영`.
- Do not use a body that is only a list of files.

## Verify After Commit
- Run `git status --short --branch`.
- Use `git log -1 --stat` or equivalent to confirm the commit contents.
- If the commit is wrong, do not rewrite history unless the user requested that action and the risk is understood.

## Push
- Before pushing, check branch and remote state:
  - `git status --short --branch`
  - `git branch --show-current`
  - `git rev-list --left-right --count @{upstream}...HEAD` when an upstream exists
- Do not force push unless explicitly requested.
- After pushing, confirm status and latest commit:
  - `git status --short --branch`
  - `git log -1 --oneline`

## Safety
- Do not install, remove, or edit `.git/hooks` unless explicitly requested.
- Do not claim full or project-wide validation unless the full/broad lane actually ran and passed on the same revision.
- Existing pre-commit behavior may run `./run_tests.sh core`; do not bypass it without explicit instruction.

## Test Worktree Safety
- Run `./run_tests.sh` from the worktree being validated.
- Confirm the script reports matching `PROJECT_PATH_WSL` and `PROJECT_PATH_WIN`.
- Do not set `PROJECT_PATH_WIN` to another worktree for commit evidence.
- If `run_tests.sh` reports a project-path mismatch, stop and report the mismatch.
- Treat config/dry-run checks as path validation only, not as test lane passes.

## Report
- Report commit SHA, branch, push target, and any tests or checks run.
- If tests were skipped, say which ones and why.
- If baseline failures exist, separate touched-cluster results from unrelated baseline failures.
