# Gameplay Shell Manual Runtime Smoke Plan

## 1. Overall Evaluation
- This smoke is now a canonical shell runtime validation in `UIAudioScene` with `onboarding` supplied through launch context.
- It protects the frozen Stage 1-9 UI architecture by validating canonical runtime composition, representative runtime ownership, and terminal stage-clear routing under actual scene conditions.
- It complements automated tests rather than replacing them.
- It must stay architecture-focused and time-bounded. It is not a general gameplay QA pass and it must not drift into exploratory playtesting.
- Claims about startup resolution flash, fullscreen/window correctness, and preview-window behavior require real-build manual validation; editor-only execution is insufficient evidence for those display-specific behaviors.

## 2. Preserved Strengths
- Preserve the canonical gameplay shell runtime focus in `UIAudioScene` with a primed stage id rather than converting this into an EditMode-only or architecture-redesign task.
- Preserve the validation targets that lower-level tests are weaker at proving: runtime layer placement, hierarchy truth, input/raycast behavior, modal feel, representative screen transitions, and terminal flow.
- Preserve `PausePopup` as the modal representative popup case.
- Preserve `TooltipPopup` as the intended non-modal representative popup case.
- Preserve the logical `ScreenId.Gameplay` root and `StageResultScreen` as architecturally sensitive screen/runtime checkpoints.
- Preserve removed diagnostics overlay absence as a root-shell/runtime-composition residue check only.
- Preserve root `UI-Current-Structure-Source.md` as the current-structure reference for the 3-layer shell, retired ActionBar status, removed diagnostics status, and canonical scene transition path.

## 3. Remaining Execution Risks
- The `SettingsScreen` tooltip info icon can regress into a broken or missing authored affordance even though the gameplay shell smoke now expects a real player-facing tooltip path.
- Stage-clear validation can expand into an unbounded gameplay session if it is not explicitly time-boxed and classified carefully.
- Removed diagnostics overlay residue can be misclassified as a runtime smoke target instead of a structural absence check.
- The highest-risk runtime subset can be crowded out unless it is executed first in a fixed order.
- Failures can be misclassified if the bounded `SettingsScreen` tooltip choice is mistaken for a universal tooltip product rule or if tooltip behavior drifts toward help-screen scale.

## 4. Required Corrections
- Split the smoke into two tiers.
- `Tier 1` is mandatory-first and must run before any secondary coverage:
  1. canonical runtime root-shell uniqueness and layer composition
  2. `ScreenId.Gameplay` as the no-view gameplay-root-adjacent special case
  3. HUD visibility and read-only behavior on the canonical path
  4. `PausePopup` as the modal representative popup
  5. `SettingsScreen` as the representative complex screen
  6. terminal `StageResultScreen` flow attempt
- `Tier 2` runs only after Tier 1 completes:
  - `ObjectiveStatusScreen`
  - `SettingsScreen`
  - `TooltipPopup` via `SettingsScreen` tooltip info icon
  - removed diagnostics overlay absence in the canonical root shell
- If Tier 1 reveals a likely structural blocker, capture evidence immediately and do not spend remaining time on secondary checks unless they are needed to disambiguate severity.

## 5. High-Risk Runtime Flow Rules
- The smoke must always execute this high-risk subset first:
  - one canonical runtime root shell and one input-routing path
  - `ScreenId.Gameplay` startup/root ownership without a visible screen prefab/view
  - HUD mounted only on `HudLayer`
  - `PausePopup` mounted only on `PopupLayer`
  - `SettingsScreen` as one runtime-owned complex screen shell
  - `StageResultScreen` as the terminal special case
- Root-shell validation uses the current-structure source: top-level canonical gameplay UI shell has `HudLayer`, `ScreenLayer`, and `PopupLayer`, with no `DiagnosticsLayer`.
- These are the primary freeze-sensitive proof points because they are most likely to expose ownership drift, duplicate roots, layer misuse, or reopened runtime seams.
- `ObjectiveStatusScreen` and `SettingsScreen` are still required representative coverage, but they are second-tier after the high-risk subset.
- Removed diagnostics overlay absence is not a first-pass manual interaction target.

## 6. Tooltip Path Classification Rules
- `TooltipPopup` remains the intended non-modal representative popup case.
- The gameplay shell smoke is now explicitly expected to expose `TooltipPopup` through the SettingsScreen tooltip info icon beside the tooltip toggle row.
- This `SettingsScreen` tooltip entry point is the current gameplay shell choice only. It must not be treated as the universal tooltip affordance pattern; future tooltip expansion requires separate plan/review.
- Valid tooltip outcome:
  - `Reachable and valid`: the `SettingsScreen` tooltip info icon opens `TooltipPopup` under `PopupLayer`, it remains non-modal, it stays tooltip-scale, and first back closes the tooltip before second back closes `SettingsScreen`.
- Classify as `Runtime integration or placement issue` when:
  - the `SettingsScreen` tooltip info icon is missing, non-functional, or routes through a non-canonical popup path
  - the tooltip opens but uses the wrong layer, dimming, lower-layer blocking, or screen-like content scale
- Escalate tooltip coverage to blocker only when:
  - the canonical `SettingsScreen` tooltip path is missing or broken in the gameplay shell
  - the tooltip path violates popup ownership, layering, dimming, lower-layer blocking, or bounded tooltip semantics
  - a legacy or duplicate popup path appears
- Do not add scene-local helpers, alternate bootstrap objects, or artificial debug triggers to satisfy tooltip coverage.
- Do not silently extend tooltip entry points to other screens as part of this smoke expectation; future tooltip expansion requires separate plan/review.

## 7. Stage-Clear Validation Rules
- Stage-clear validation remains mandatory for the overall manual runtime freeze decision, but the smoke must execute it in a bounded way.
- Use a hard limit of:
  - up to 3 deliberate attempts from fresh gameplay state, or
  - up to 10 focused minutes on the terminal-path portion of the smoke,
  whichever comes first.
- During those bounded attempts, collect enough evidence to separate non-reachability from broken routing:
  - whether the objective path appears understandable in current gameplay
  - whether `ObjectiveStatusScreen` reflects progress consistently
  - whether the player ever appears to satisfy final objective state without transition
- Classify stage-clear outcomes as:
  - `Pass`: stage clear is reached and routes only to `StageResultScreen` with sane HUD/popup/screen behavior.
  - `Runtime integration or placement issue`: the player appears to satisfy the authored objective, or runtime objective state indicates clear readiness, but `StageResultScreen` does not appear, a legacy overlay appears, the stack is wrong, or the transition is visibly broken.
  - `Inconclusive/manual follow-up needed`: the bounded smoke could not practically reach terminal state, but there is no direct evidence that terminal routing is broken.
- `Inconclusive/manual follow-up needed` is not proof of architecture regression, but it does leave the manual runtime freeze gate open until a targeted terminal-flow follow-up validates `StageResultScreen`.
- Do not let stage-clear pursuit turn the smoke into a general gameplay playtest.

## 8. Removed Diagnostics Overlay Rules
- Diagnostics overlay is a removed unused runtime feature, not a dev-only retained smoke target.
- Manual smoke should not attempt F3/F4 diagnostics overlay interaction.
- Treat any `DiagnosticsLayer`, diagnostics overlay object, or F3/F4 diagnostics overlay path in canonical runtime UI as a structural residue finding.

## 9. Evidence and Failure Classification Rules
- Every finding must be classified as one of:
  - `Architecture regression`
  - `Runtime integration or placement issue`
  - `Scene-affordance coverage gap`
  - `Presentation / UX tuning issue`
  - `Inconclusive/manual follow-up needed`
- Evidence must stay proportional to failure type:
  - structural/runtime placement failures: mandatory Hierarchy plus Game-view capture
  - popup/screen/terminal flow failures: mandatory exact repro steps, start state, input sequence, expected result, and actual result
  - scene-affordance coverage gaps: brief note of what representative path was sought, where it was expected, and why the scene did not expose it
  - removed diagnostics overlay residue findings: record hierarchy/source evidence and classify as structural residue
- Record `could not reach terminal state within bounded attempts` as its own outcome. Do not collapse it into `terminal flow broken`.
- For high-risk subset failures, always note whether the problem reproduces on first attempt or only intermittently.

## 10. Freeze Gate
- The manual runtime smoke is only freezeable if the high-risk runtime subset is executed first and validated before lower-priority checks consume time.
- Tooltip representative coverage is acceptable only if the `SettingsScreen` tooltip info icon reaches `TooltipPopup` on the canonical gameplay shell path.
- Stage-clear validation remains mandatory, but it must stay operationally bounded. If the result is `Inconclusive/manual follow-up needed`, that is not an architecture verdict, but the manual runtime freeze gate remains open until terminal flow is proven.
- Diagnostics overlay must remain absent from canonical runtime UI.
- The `SettingsScreen` tooltip affordance must remain bounded. It is the current gameplay shell choice, not a universal tooltip rule for every screen.
- Click-only open and center anchoring remain current-task defaults for this gameplay shell affordance, not universal architecture laws.
- Structural regressions, runtime integration issues, scene-affordance gaps, inconclusive bounded outcomes, and pure presentation/tuning issues must remain clearly separated.
- The smoke is acceptable only if it stays architecture-focused and time-bounded rather than expanding into open-ended scene playtesting.
