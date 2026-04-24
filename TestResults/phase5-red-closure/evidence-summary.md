# Phase 5 Red-Closure Evidence Summary

- Base Revision: `6de8c731a1c8076d9f5091e17158b923abf265ad`
- Executed At (UTC): `2026-04-24 13:07:35Z`
- Scope: `WindupMelee RandomWalk pilot` same-revision targeted Unity evidence
- Verdict: `Close Retry Ready`
- Final Decision: `Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md`
- Working Tree Scope: `Docs/Architecture/*`, `EnemyPatrolPhase5DocumentationTests.cs`, `TestResults/phase5-red-closure/*`; runtime gameplay code unchanged

## Gate Status

| gate | status | evidence | note |
| --- | --- | --- | --- |
| baseline control | green | `baseline-control-targeted.xml` | targeted 2 / 2 passed |
| runtime diagnostics | green | `unit-runtime-targeted.xml` | targeted 3 / 3 passed; `AttackCommitted` gate split, patrol ownership triage passed |
| runtime scenario | green | `scenario-targeted.xml` | targeted 4 / 4 passed; direct/open/lose-target/same-cell ordering all passed |
| replay / determinism | green | `replay-targeted.xml` | targeted 5 / 5 passed |
| authoring / documentation governance | green | `unit-authoring-doc-targeted.xml` | targeted 10 / 10 passed |

## Runtime Red Closure

| lane | status | evidence | close note |
| --- | --- | --- | --- |
| `AttackCommitted` | green | `unit-runtime-targeted.xml`, `scenario-targeted.xml`, `attackcommitted-gate-summary.txt` | start / execute / commit / recover lane restored under bounded windup carve-out |
| `pilot lose-target active-windup entry` | green | `scenario-targeted.xml`, `lockedtargetlost-conditional-summary.txt` | runtime cancel / wording / home-return are green; scenario resumed-patrol window is isolated from player respawn reacquisition |
| `duplicate CommittedMove` | green | `unit-runtime-targeted.xml`, `patrol-write-triage-summary.txt` | actual patrol write = 1, semantic correlation = 1, trace duplicate removed from formatter output |

## Artifact Inventory

- `baseline-control-targeted.xml`
- `baseline-control-targeted.log`
- `unit-runtime-targeted.xml`
- `unit-runtime-targeted.log`
- `scenario-targeted.xml`
- `scenario-targeted.log`
- `replay-targeted.xml`
- `replay-targeted.log`
- `unit-authoring-doc-targeted.xml`
- `unit-authoring-doc-targeted.log`
- `attackcommitted-gate-summary.txt`
- `lockedtargetlost-conditional-summary.txt`
- `patrol-write-triage-summary.txt`

## Retry Boundary

- 이 summary는 `phase 5 close` 자체를 선언하지 않는다.
- 이 summary는 same-revision targeted evidence 기준으로 `phase 5 close retry`를 다시 시도할 수 있음을 기록한다.
- official close decision is recorded in `Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md`.
- `lose-target-debug.xml/.log`는 historical diagnostic appendix이며 close gate 필수 inventory가 아니다.
- baseline fallback/oracle, `Forward`, `NonAttacking`, other archetype patrol policy no-touch 전제는 유지된다.
