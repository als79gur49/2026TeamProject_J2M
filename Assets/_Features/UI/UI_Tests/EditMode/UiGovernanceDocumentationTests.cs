using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class UiGovernanceDocumentationTests
    {
        [Test]
        public void UiBaselineNote_RecordsStructuralEvolutionSections_AndGuardGovernance()
        {
            var baseline = ReadRepoFile("Docs/Testing/UI-EditMode-Baseline-2026-04-15.md");

            Assert.That(baseline, Does.Contain("# UI EditMode Baseline 2026-04-15"));
            Assert.That(baseline, Does.Contain("## Scope"));
            Assert.That(baseline, Does.Contain("## Result"));
            Assert.That(baseline, Does.Contain("## Structural Delta"));
            Assert.That(baseline, Does.Contain("## Guard Evolution"));
            Assert.That(baseline, Does.Contain("## Runner Warning Status"));
            Assert.That(baseline, Does.Contain("## PlayMode Escalation"));
            Assert.That(baseline, Does.Contain("## Covered Freeze Evidence"));
            Assert.That(baseline, Does.Contain("## Companion Smoke Check"));
            Assert.That(baseline, Does.Contain("Added tests"));
            Assert.That(baseline, Does.Contain("Removed tests"));
            Assert.That(baseline, Does.Contain("Renamed / merged / split tests"));
            Assert.That(baseline, Does.Contain("Replaced weak guards"));
            Assert.That(baseline, Does.Contain("Obsolete guards"));
            Assert.That(baseline, Does.Contain("Runner warning changes"));
            Assert.That(baseline, Does.Contain("Public-surface change governance"));
            Assert.That(baseline, Does.Not.Contain("Hud:PersistentHud -> GameplayLegacyHudViewFactory.Create"));
            Assert.That(baseline, Does.Not.Contain("removal and unrelated migration/helper cleanup remain later work"));
            Assert.That(baseline, Does.Not.Contain("Popup:Pause -> GameplayPopupRuntimeFactory.CreatePausePopup"));
        }

        [Test]
        public void GameplayTestAutomationGuide_UsesStageNineUiLanguage_AndRemovesStaleUiCounts()
        {
            var guide = ReadRepoFile("Docs/Testing/Gameplay-Test-Automation-Guide.md");

            Assert.That(guide, Does.Contain("Stage 9"));
            Assert.That(guide, Does.Contain("UI hardening"));
            Assert.That(guide, Does.Contain("PlayMode escalation triggers"));
            Assert.That(guide, Does.Contain("UI-EditMode-Baseline-2026-04-15.md"));
            Assert.That(guide, Does.Contain("GameplayShell-Manual-Runtime-Smoke-Plan.md"));
            Assert.That(guide, Does.Contain("Display-Settings-Build-Validation-Checklist.md"));
            Assert.That(guide, Does.Contain("targeted display architecture validated"));
            Assert.That(guide, Does.Contain("real-build manual display validation completed"));
            Assert.That(guide, Does.Contain("Editor-only execution is insufficient evidence for fullscreen/window correctness."));
            Assert.That(guide, Does.Contain("Climate PR2 code-head reference green on 2026-07-26 KST"));
            Assert.That(guide, Does.Contain("Windows UI build `0` errors"));
            Assert.That(guide, Does.Contain("Unity UI EditMode `1060 total / 0 failed`"));
            Assert.That(guide, Does.Contain("CommandLine-20260726-052954"));
            Assert.That(guide, Does.Contain("three separate `Diagnostics/` PNGs"));
            Assert.That(guide, Does.Contain("UI-Current-Structure-Source.md"));
            Assert.That(guide, Does.Contain("current UI structure or stale-token audit policy changes"));
            Assert.That(guide, Does.Contain("2차 UI canonical 보정 보고서에 기록된 UI red 사유"));
            Assert.That(guide, Does.Contain("SurfaceBeltButtonBadgeStyleProfile"));
            Assert.That(guide, Does.Contain("SurfaceBeltButtonBadgeGroupView"));
            Assert.That(guide, Does.Contain("EnemyTargetEligibilityResult"));
            Assert.That(guide, Does.Contain("PendingEnemyBlockedReaction"));
            Assert.That(guide, Does.Contain("UI deletion candidates are removed only when the product decision, current lane evidence, and baseline note update land in the same change."));
            Assert.That(guide, Does.Not.Contain("`46 total / 0 failed`"));
        }

        [Test]
        public void UiArchitectureGuidelines_CanonicalizePresenterOwnership_AsApplicationLayer()
        {
            var guidelines = ReadRepoFile("Docs/Architecture/UI-Architecture-Guidelines.md");

            Assert.That(guidelines, Does.Contain("UI use cases, presenter-level application orchestration, and UI-facing intent routing"));
            Assert.That(guidelines, Does.Contain("screen-specific payloads, viewmodels, views, prefabs, and screen-local UI types"));
            Assert.That(guidelines, Does.Contain("popup-specific payloads, viewmodels, views, prefabs, and popup-local UI types"));
            Assert.That(guidelines, Does.Contain("persistent HUD-specific viewmodels, views, prefabs, and HUD-local UI types"));
            Assert.That(guidelines, Does.Contain("`UI_Application` owns presenter-level orchestration for screen, popup, and HUD slices."));
            Assert.That(guidelines, Does.Contain("`UI_Screens`, `UI_Popups`, and `UI_HUD` own view-facing feature assets and local UI types; they do not own application-layer presenter orchestration."));
            Assert.That(guidelines, Does.Contain("`UI_Composition` instantiates presenters and binds them to canonical views at runtime."));
            Assert.That(guidelines, Does.Contain("UI SFX ownership is split between coordinator-owned flow outcome cues, local widget/HUD cues, and transition-overlay whitelist cues"));
            Assert.That(guidelines, Does.Contain("the coordinator transaction/outcome layer is the only flow-cue trigger seam"));
            Assert.That(guidelines, Does.Contain("Audio-Architecture-Guidelines.md"));
            Assert.That(guidelines, Does.Contain("Current canonical identity lists"));
            Assert.That(guidelines, Does.Contain("UI-Current-Structure-Source.md"));
            Assert.That(guidelines, Does.Contain("Scene transition semantic ids remain distinct, but semantic ids and physical content prefab files are not one-to-one."));
            Assert.That(guidelines, Does.Contain("`StageClearNext` resolves exactly to `GenericLoadingOverlayContent`; the content is the authored StageAdvance primitive and is not a generic fallback."));
            Assert.That(guidelines, Does.Contain("`DeathRetryChanceLost` resolves exactly to the dedicated `ChanceLostOverlayContent` path"));
            Assert.That(guidelines, Does.Contain("Other canonical transition kinds use their typed Iris executor and do not infer catalog content from an overlay kind or fallback entry."));
            Assert.That(guidelines, Does.Contain("chance-loss visuals are slot/effect-driven and do not expose dynamic previous/current/total/death chance text bindings"));
            Assert.That(guidelines, Does.Contain("current authored slots use explicit inspector-bound slot roots"));
            Assert.That(guidelines, Does.Contain("`ChanceSlotView*` name fallback exists only as a safety net"));
            Assert.That(guidelines, Does.Contain("Scene transition content base views expose only root group and progress text as required inspector bindings; title/message/progress bar/animator base bindings are not current contract."));
            Assert.That(guidelines, Does.Contain("`LevelFailedRestart` does not own a current dedicated transition message/text content contract."));
            Assert.That(guidelines, Does.Contain("`StageResult`, `LevelFailed`, and `GameClear` are canonical terminal result screens."));
            Assert.That(guidelines, Does.Contain("`GameClear` is a result-only terminal screen with title and main label bindings only; retired authored restart/detail compatibility objects are not current contract."));
            Assert.That(guidelines, Does.Contain("`Help` and `Inventory` are not current gameplay screens."));
            Assert.That(guidelines, Does.Contain("`DemoStageControl` is not a gameplay popup catalog entry."));
            Assert.That(guidelines, Does.Contain("catalog-less runtime assist popup created through the factory/runtime/hotkey path"));
            Assert.That(guidelines, Does.Contain("build-included tester/demo/showcase assist feature"));
            Assert.That(guidelines, Does.Contain("tester assist clear, hard-section bypass, showcase navigation, and stage browsing"));
            Assert.That(guidelines, Does.Contain("not a deletion candidate and is not a dev-only compile exclusion target"));
            Assert.That(guidelines, Does.Contain("separate product/build configuration decision"));
            Assert.That(guidelines, Does.Contain("not by a simple `DEVELOPMENT_BUILD` or `UNITY_EDITOR` compile gate"));
            AssertDemoStageControlStalePolicyPhrasesAreAbsent(guidelines);
            Assert.That(guidelines, Does.Contain("canonical runtime-bound HUD members are `Pause`, `StageInfo`, `ObjectiveHud`, `ChancePanel`, `SurfaceBeltIndicator`, and `PlayerStatus`"));
            Assert.That(guidelines, Does.Contain("removed as retired HUD proof residue"));
            Assert.That(guidelines, Does.Contain("Do not delete `LevelFailed`, `GameClear`, `StageResult`, `Confirm` popup, `UI_Composition` adapters, UI audio/display/settings bridge code, or the `StageNavigationRequest` path"));
            Assert.That(guidelines, Does.Contain("Stage clear routes through `MinimalStageCompletionReadModel -> StageResult`."));
            Assert.That(guidelines, Does.Contain("`StageResult` is a minimal stage-completion navigation endpoint"));
            Assert.That(guidelines, Does.Contain("Its payload does not carry title/summary/detail result schema; the localized stage-clear title is presentation-owned"));
            Assert.That(guidelines, Does.Contain("UI diagnostics overlay was removed as an unused runtime feature after an explicit owner decision."));
            Assert.That(guidelines, Does.Contain("Canonical runtime UI must not include a diagnostics overlay, `DiagnosticsLayer`, or F3/F4 diagnostics input path."));
            Assert.That(guidelines, Does.Not.Contain("Diagnostics overlay is also not a deletion-safe item in this phase."));
            Assert.That(guidelines, Does.Contain("This deletion decision does not change Push/Flip readiness mapping or gameplay command ownership."));
            Assert.That(
                guidelines,
                Does.Contain("PausePopup completion semantics are coordinator-owned: Resumed and Closed are resume-equivalent exits, while SettingsRequested keeps gameplay paused, opens the settings screen, and returns back to a fresh PausePopup."));
            Assert.That(guidelines, Does.Not.Contain("screen-specific presenters, viewmodels, views, and screen composition"));
            Assert.That(guidelines, Does.Not.Contain("popup-specific presenters, viewmodels, views, and popup composition"));
            Assert.That(guidelines, Does.Not.Contain("persistent HUD-specific presenters, viewmodels, views, and HUD composition"));
        }

        [Test]
        public void UiBaselineNote_RecordsPhaseOneCanonicalDriftCorrections_AndDeletionProtections()
        {
            var baseline = ReadRepoFile("Docs/Testing/UI-EditMode-Baseline-2026-04-15.md");

            Assert.That(baseline, Does.Contain("Prior Phase 1 drift-correction rerun: green on 2026-06-06 KST"));
            Assert.That(baseline, Does.Contain("Current PR-A Objective UI removal baseline rerun: green on 2026-06-11 KST"));
            Assert.That(baseline, Does.Contain("Current PR-T3 transition content base contract rerun: green on 2026-06-12 KST"));
            Assert.That(baseline, Does.Contain("Current PR-T5 ChanceLost slot root explicit binding rerun: green on 2026-06-12 KST"));
            Assert.That(baseline, Does.Contain("Current StageResult result text schema cleanup rerun: green on 2026-06-12 KST"));
            Assert.That(baseline, Does.Contain("Current Windows build result: `dotnet build Game.Feature.UI.Tests.csproj -c Debug` passed with `0` errors"));
            var resultSection = ExtractMarkdownSection(baseline, "## Result");
            Assert.That(resultSection, Does.Contain("Current Unity UI EditMode: `1346 total / 0 failed`"));
            Assert.That(resultSection, Does.Contain("result `1346 total / 0 failed`, failed tests `none`, failure category `none`"));
            Assert.That(resultSection, Does.Contain("CommandLine-20260726-052954"));
            Assert.That(resultSection, Does.Not.Contain("706 total / 0 failed"), "Current baseline Result section must not retain stale 706 total evidence.");
            Assert.That(baseline, Does.Contain("PR-A Objective UI removal guards proving `ObjectiveStatus` screen, `ObjectiveInfo` popup, pause objective action semantics, deleted prefab files, and deleted prefab GUID references are absent from production UI vocabulary"));
            Assert.That(baseline, Does.Contain("external structure-source regeneration guard"));
            Assert.That(baseline, Does.Contain("root `UI-Current-Structure-Source.md` is the external current-structure source"));
            Assert.That(baseline, Does.Contain("canonical UI navigation resolver guards"));
            Assert.That(baseline, Does.Contain("PR-T2 transition content guards proving `StageClearNext` maps exactly to authored `GenericLoadingOverlayContent`, `DeathRetryChanceLost` maps exactly to dedicated `ChanceLostOverlayContent`, unmatched transition kinds fail closed, stale LevelFailed-only transition message/text residue is removed, and deleted duplicate content prefab GUID references are absent"));
            Assert.That(baseline, Does.Contain("PR-T3 transition content base contract guards proving base content requires only root group and progress text bindings, while retired title/message/progress bar/animator base bindings stay absent from source and prefabs"));
            Assert.That(baseline, Does.Contain("PR-T5 ChanceLost slot root binding guards proving `_chanceSlotRoots` remains the explicit inspector binding contract"));
            Assert.That(baseline, Does.Contain("Duplicate common transition content prefab files and stale common-only content view types were removed after PR-T2 collapsed the shared physical content mapping."));
            Assert.That(baseline, Does.Contain("renamed the transition content catalog guard to cover shared semantic mapping instead of one physical prefab per semantic"));
            Assert.That(baseline, Does.Contain("scene transition content mapping is exact: `StageClearNext` uses authored `GenericLoadingOverlayContent`, `DeathRetryChanceLost` uses slot/effect-driven `ChanceLostOverlayContent`, unmatched kinds fail closed, and no overlay-kind or generic catalog fallback participates in production routing"));
            Assert.That(baseline, Does.Contain("`LevelFailedRestart` has no current dedicated transition content message/text contract; the old LevelFailed-only message field was removed as stale residue"));
            Assert.That(baseline, Does.Contain("legacy navigation router setup coverage is replaced with `IUiNavigationTargetResolver` fixture coverage plus public-surface absence guards"));
            Assert.That(baseline, Does.Contain("Prior 2차 UI canonical correction report red reason"));
            Assert.That(baseline, Does.Contain("SurfaceBeltButtonBadgeStyleProfile"));
            Assert.That(baseline, Does.Contain("SurfaceBeltButtonBadgeGroupView"));
            Assert.That(baseline, Does.Contain("EnemyTargetEligibilityResult"));
            Assert.That(baseline, Does.Contain("PendingEnemyBlockedReaction"));
            Assert.That(baseline, Does.Contain("retired HUD proof residue was removed after product option B was selected"));
            Assert.That(baseline, Does.Contain("Help is also not a current gameplay screen"));
            Assert.That(baseline, Does.Contain("current canonical `ScreenId` values are `None`, `Gameplay`, `Settings`, `StageResult`, `LevelFailed`, and `GameClear`"));
            Assert.That(baseline, Does.Contain("`StageResultScreen`, `LevelFailedScreen`, and `GameClearScreen` remain runtime-owned terminal result screens"));
            Assert.That(baseline, Does.Contain("`GameClearScreen` is a result-only terminal screen with title and main label bindings only; retired authored `RestartLevelButton` and `Detail` compatibility objects were removed from its runtime view and prefab"));
            Assert.That(baseline, Does.Contain("current canonical `PopupId` values are `None`, `Pause`, `Confirm`, and `DemoStageControl`"));
            Assert.That(baseline, Does.Contain("`DemoStageControl` is a catalog-less runtime assist popup created through the factory/runtime/hotkey path and not a gameplay popup catalog entry"));
            Assert.That(baseline, Does.Contain("`DemoStageControl` is a build-included tester/demo/showcase assist feature"));
            Assert.That(baseline, Does.Contain("tester assist clear, hard-section bypass, showcase navigation, and stage browsing"));
            Assert.That(baseline, Does.Contain("it is not a deletion candidate or dev-only compile exclusion target"));
            Assert.That(baseline, Does.Contain("future public-release hiding or disabling for `DemoStageControl` requires a separate product/build configuration decision"));
            Assert.That(baseline, Does.Contain("not a simple `DEVELOPMENT_BUILD` or `UNITY_EDITOR` compile gate"));
            Assert.That(baseline, Does.Contain("`Pause` and `Confirm` remain protected canonical popup paths; Reward popup is absent from current popup vocabulary and is not the canonical stage-clear result path"));
            Assert.That(baseline, Does.Contain("`TooltipPopup` was retired from the current popup vocabulary after PR-TT1 found no production caller."));
            Assert.That(baseline, Does.Contain("Settings display hover hint remains as a local inline pointer-hover affordance and does not use `PopupId.Tooltip`."));
            Assert.That(baseline, Does.Contain("canonical HUD composition is `Pause`, `StageInfo`, `ObjectiveHud`, `ChancePanel`, `SurfaceBeltIndicator`, and `PlayerStatus`"));
            Assert.That(baseline, Does.Contain("UI diagnostics overlay was removed as an unused runtime feature; it is not hidden, dev-only retained, or a protected runtime path"));
            Assert.That(baseline, Does.Contain("protected UI paths for drift correction include `LevelFailed`, `GameClear`, `StageResult`, Pause/Confirm popup paths, `UI_Composition` adapters, UI audio/display/settings bridges, and `StageNavigationRequest`"));
            Assert.That(baseline, Does.Not.Contain("diagnostics overlay pending a separate production/dev-only policy decision"));
            AssertDemoStageControlStalePolicyPhrasesAreAbsent(baseline);
            Assert.That(baseline, Does.Not.Contain("HelpScreen remains"));
            Assert.That(baseline, Does.Not.Contain("InventoryScreen remains"));
        }

        [Test]
        public void UiBaselineNote_RecordsPrOneStageCompletionProtection_AndDeferredPolicyExtraction()
        {
            var baseline = ReadRepoFile("Docs/Testing/UI-EditMode-Baseline-2026-04-15.md");

            Assert.That(baseline, Does.Contain("PR-1 stage completion guards proving StageResult + Continue, final-stage GameClear, retry payload, next-stage/no-next-stage mapping, terminal back consume, Reward popup absence, and screen/popup/HUD separation before UI refactor scaffolding begins"));
            Assert.That(baseline, Does.Contain("StageResult screen is a minimal stage-completion navigation endpoint"));
            Assert.That(baseline, Does.Contain("It no longer carries or displays title/summary/detail result text"));
            Assert.That(baseline, Does.Contain("emits intent-only `StageNavigationRequest` values for continue, retry, and next-stage paths"));
            Assert.That(baseline, Does.Contain("Reward popup is not a stage-clear presentation endpoint or reward commit owner"));
            Assert.That(baseline, Does.Contain("Reward/progression commit remains owned by the stage subsystem"));
            Assert.That(baseline, Does.Contain("UI remains non-authoritative"));
            Assert.That(baseline, Does.Contain("does not mutate `WorldState`"));
            Assert.That(baseline, Does.Contain("does not receive raw `TickResult` or raw gameplay frames in views"));
            Assert.That(baseline, Does.Contain("consumes snapshots/viewmodels/read models instead"));
            Assert.That(baseline, Does.Contain("terminal result screens consume back"));
            Assert.That(baseline, Does.Not.Contain("Reward popup consumes back through popup policy until acknowledged"));
            Assert.That(baseline, Does.Contain("screen/popup/HUD remain separate stacks/layers with input blocking derived from `UIBlockPolicy`"));
            Assert.That(baseline, Does.Contain("Final-stage clear currently selects `GameClear` instead of the regular StageResult next-stage flow"));
            Assert.That(baseline, Does.Contain("Policy extraction candidates for later PR: terminal screen selection, terminal back handling, pause return decision, audio transaction outcome mapping, and final-stage routing. Do not extract in PR-1."));
            Assert.That(baseline, Does.Contain("Remaining PR-1 gaps: builder registry completeness belongs to PR-2; popup completion audio mapping, settings adapter lifecycle, HUD module completeness, and broader stage completion end-to-end/manual runtime evidence remain follow-up work."));
        }

        [Test]
        public void GameplayShellManualRuntimeSmokePlan_PreservesBoundedArchitectureFocusedSections()
        {
            var smokePlan = ReadRepoFile("Docs/Testing/GameplayShell-Manual-Runtime-Smoke-Plan.md");

            Assert.That(smokePlan, Does.Contain("# Gameplay Shell Manual Runtime Smoke Plan"));
            Assert.That(smokePlan, Does.Contain("## 1. Overall Evaluation"));
            Assert.That(smokePlan, Does.Contain("## 2. Preserved Strengths"));
            Assert.That(smokePlan, Does.Contain("## 3. Remaining Execution Risks"));
            Assert.That(smokePlan, Does.Contain("## 4. Required Corrections"));
            Assert.That(smokePlan, Does.Contain("## 5. High-Risk Runtime Flow Rules"));
            Assert.That(smokePlan, Does.Contain("## 6. TooltipPopup Retirement Rules"));
            Assert.That(smokePlan, Does.Contain("## 7. Stage-Clear Validation Rules"));
            Assert.That(smokePlan, Does.Contain("## 8. Removed Diagnostics Overlay Rules"));
            Assert.That(smokePlan, Does.Contain("## 9. Evidence and Failure Classification Rules"));
            Assert.That(smokePlan, Does.Contain("## 10. Freeze Gate"));
            Assert.That(smokePlan, Does.Contain("Tier 1"));
            Assert.That(smokePlan, Does.Contain("ScreenId.Gameplay"));
            Assert.That(smokePlan, Does.Contain("PausePopup"));
            Assert.That(smokePlan, Does.Contain("SettingsScreen"));
            Assert.That(smokePlan, Does.Contain("StageResultScreen"));
            Assert.That(smokePlan, Does.Contain("`TooltipPopup` is not a current popup vocabulary member after PR-TT1 found no production caller."));
            Assert.That(smokePlan, Does.Contain("Settings display hover hint remains a local inline pointer-hover affordance and does not use `PopupId.Tooltip`."));
            Assert.That(smokePlan, Does.Contain("Removed Settings accessibility toggle rows must not be treated as required manual-smoke affordances."));
            Assert.That(smokePlan, Does.Contain("must not restore Settings tooltip on/off or large text toggle rows"));
            Assert.That(smokePlan, Does.Contain("Treat any `PopupId.Tooltip`, `TooltipPopup` prefab, catalog field, factory case, presenter, viewmodel, or view in the current runtime path as contract residue."));
            Assert.That(smokePlan, Does.Contain("Inconclusive/manual follow-up needed"));
            Assert.That(smokePlan, Does.Contain("up to 3 deliberate attempts"));
            Assert.That(smokePlan, Does.Contain("up to 10 focused minutes"));
            Assert.That(smokePlan, Does.Contain("Diagnostics overlay is a removed unused runtime feature"));
            Assert.That(smokePlan, Does.Contain("Manual smoke should not attempt F3/F4 diagnostics overlay interaction."));
            Assert.That(smokePlan, Does.Contain("UI-Current-Structure-Source.md"));
            Assert.That(smokePlan, Does.Contain("top-level canonical gameplay UI shell has `HudLayer`, `ScreenLayer`, and `PopupLayer`, with no `DiagnosticsLayer`"));
            Assert.That(smokePlan, Does.Contain("architecture-focused"));
            Assert.That(smokePlan, Does.Contain("Do not add scene-local helpers"));
            Assert.That(smokePlan, Does.Contain("artificial debug triggers"));
            Assert.That(smokePlan, Does.Contain("editor-only execution is insufficient evidence"));
        }

        [Test]
        public void UiCurrentStructureSource_RecordsCleanupWaveCanonicalState_AndStaleTokenPolicy()
        {
            var source = ReadRepoFile("UI-Current-Structure-Source.md");

            Assert.That(source, Does.Contain("# UI Current Structure Source"));
            Assert.That(source, Does.Contain("`HudLayer`"));
            Assert.That(source, Does.Contain("`ScreenLayer`"));
            Assert.That(source, Does.Contain("`PopupLayer`"));
            Assert.That(source, Does.Contain("`DiagnosticsLayer` is absent."));
            Assert.That(source, Does.Contain("`None`"));
            Assert.That(source, Does.Contain("`Gameplay`"));
            Assert.That(source, Does.Not.Contain("`ObjectiveStatus`"));
            Assert.That(source, Does.Contain("`Settings`"));
            Assert.That(source, Does.Contain("`StageResult`"));
            Assert.That(source, Does.Contain("`StageResult` is a minimal stage-completion navigation endpoint"));
            Assert.That(source, Does.Contain("It no longer carries or displays title/summary/detail result text"));
            Assert.That(source, Does.Contain("`LevelFailed`"));
            Assert.That(source, Does.Contain("`GameClear`"));
            Assert.That(source, Does.Contain("`Pause`"));
            Assert.That(source, Does.Not.Contain("`ObjectiveInfo`"));
            Assert.That(source, Does.Contain("`Confirm`"));
            Assert.That(source, Does.Not.Contain("  - `Tooltip`"));
            Assert.That(source, Does.Contain("`TooltipPopup` was retired from the current popup vocabulary after PR-TT1 found no production caller."));
            Assert.That(source, Does.Contain("Settings display hover hint remains as a local inline pointer-hover affordance and does not use `PopupId.Tooltip`."));
            Assert.That(source, Does.Contain("Reward popup is not current popup vocabulary."));
            Assert.That(source, Does.Contain("Stage reward/progression vocabulary remains stage-owned content/system vocabulary"));
            Assert.That(source, Does.Contain("`GameClear` is a result-only terminal screen with title and main label bindings only; retired authored restart/detail compatibility objects are not current contract."));
            Assert.That(source, Does.Contain("`DemoStageControl` is not a gameplay popup catalog entry."));
            Assert.That(source, Does.Contain("catalog-less runtime assist popup"));
            Assert.That(source, Does.Contain("build-included tester/demo/showcase assist feature"));
            Assert.That(source, Does.Contain("not a deletion candidate and is not a dev-only compile exclusion target"));
            Assert.That(source, Does.Contain("`ActionBar` is removed retired HUD proof residue."));
            Assert.That(source, Does.Contain("`Help` and `Inventory` are not current gameplay screens."));
            Assert.That(source, Does.Contain("no `UiArchitectureDiagnostics`"));
            Assert.That(source, Does.Contain("no `DiagnosticsOverlay`"));
            Assert.That(source, Does.Contain("no F3/F4 diagnostics overlay input path"));
            Assert.That(source, Does.Contain("`SceneTransitionOverlayShell` plus `SceneTransitionOverlayContentCatalog`"));
            Assert.That(source, Does.Contain("Scene transition semantic ids are preserved, but semantic ids and physical content prefab files are not one-to-one."));
            Assert.That(source, Does.Contain("`StageClearNext` resolves exactly to the physical `GenericLoadingOverlayContent` prefab; this is the authored StageAdvance content primitive, not a fallback."));
            Assert.That(source, Does.Contain("`DeathRetryChanceLost` resolves exactly to the dedicated `ChanceLostOverlayContent` prefab"));
            Assert.That(source, Does.Contain("Other canonical transition kinds use typed Iris executors and do not infer content through an overlay-kind or generic catalog fallback."));
            Assert.That(source, Does.Contain("slot/effect-driven chance-loss visuals and does not expose dynamic previous/current/total/death chance text bindings"));
            Assert.That(source, Does.Contain("current authored slots use explicit inspector-bound slot roots"));
            Assert.That(source, Does.Contain("`ChanceSlotView*` name fallback exists only as a safety net"));
            Assert.That(source, Does.Contain("Scene transition content base views expose only root group and progress text as required inspector bindings; title/message/progress bar/animator base bindings are not current contract."));
            Assert.That(source, Does.Contain("`LevelFailedRestart` has no current dedicated message/text content contract"));
            Assert.That(source, Does.Contain("`SceneTransitionOverlayView`, `UI/SceneTransitionOverlayView`, generated fallback, and legacy overlay fallback are not current paths."));
            Assert.That(source, Does.Contain("resolver-only input router initialized through `IUiNavigationTargetResolver`"));
            Assert.That(source, Does.Contain("must not regain `PopupController`, `PopupLayerView`, or `MainMenuScreenView` direct legacy overloads"));
            Assert.That(source, Does.Contain("Settings tooltip on/off and large text on/off accessibility toggles are removed residue."));
            Assert.That(source, Does.Contain("`AccessibilitySettingsStore` is not a current runtime composition dependency."));
            Assert.That(source, Does.Contain("Do not modify runtime code for this source regeneration."));
            Assert.That(source, Does.Contain("Do not modify prefabs or catalogs for this source regeneration."));
            Assert.That(source, Does.Contain("Do not simplify or reroute StageResult, Pause/Confirm popup, settings, audio, display, or UI bridge paths."));
            Assert.That(source, Does.Contain("Do not restore Settings tooltip on/off or large text on/off toggles without a separate product decision."));
            AssertDemoStageControlStalePolicyPhrasesAreAbsent(source);
            Assert.That(source, Does.Not.Contain("HelpScreen remains"));
            Assert.That(source, Does.Not.Contain("InventoryScreen remains"));
            Assert.That(source, Does.Not.Contain("ActionBar remains"));
            Assert.That(source, Does.Not.Contain("Diagnostics overlay is also not a deletion-safe item in this phase."));
        }

        [Test]
        public void DisplaySettingsGuidelines_AndBuildChecklist_RecordBootLifecycleAndRealBuildRules()
        {
            var displayGuidelines = ReadRepoFile("Docs/Architecture/Display-Settings-V1-Guidelines.md");
            var buildChecklist = ReadRepoFile("Docs/Testing/Display-Settings-Build-Validation-Checklist.md");

            Assert.That(displayGuidelines, Does.Contain("DisplayRuntimeInstaller"));
            Assert.That(displayGuidelines, Does.Contain("same canonical bootstrap root"));
            Assert.That(displayGuidelines, Does.Contain("Refresh remains internal-only in v1"));
            Assert.That(displayGuidelines, Does.Contain("preview timeout seconds"));
            Assert.That(displayGuidelines, Does.Contain("Do not add a scene-global fallback lookup."));
            Assert.That(displayGuidelines, Does.Contain("## Settings Authored Child-View Checklist"));
            Assert.That(displayGuidelines, Does.Contain("## Settings Preview Countdown Policy"));
            Assert.That(displayGuidelines, Does.Contain("SettingsAudioSection"));
            Assert.That(displayGuidelines, Does.Contain("SettingsDisplaySection"));
            Assert.That(displayGuidelines, Does.Contain("SettingsScreenView` must serialize `_audioView` and `_displayView` directly"));
            Assert.That(
                displayGuidelines,
                Does.Contain("The Settings resolution hover hint is a local SettingsDisplaySection affordance and does not use TooltipPopup or popup flow."));
            Assert.That(
                displayGuidelines,
                Does.Contain("Removed Settings accessibility toggles such as tooltip on/off and large text on/off must not be restored as part of display settings work."));
            Assert.That(displayGuidelines, Does.Contain("DisplayPreviewSessionHost"));
            Assert.That(displayGuidelines, Does.Contain("confirm popup open succeeds"));
            Assert.That(displayGuidelines, Does.Contain("whole-second stepwise text plus bar"));
            Assert.That(displayGuidelines, Does.Contain("does not add live popup countdown UI"));
            Assert.That(displayGuidelines, Does.Not.Contain("TooltipPopup auto-hide"));
            Assert.That(displayGuidelines, Does.Not.Contain("remain later work"));
            Assert.That(buildChecklist, Does.Contain("Editor-only execution is insufficient evidence for fullscreen/window correctness."));
            Assert.That(buildChecklist, Does.Contain("startup apply"));
            Assert.That(buildChecklist, Does.Contain("timeout revert"));
            Assert.That(buildChecklist, Does.Contain("alt-tab"));
            Assert.That(buildChecklist, Does.Contain("CanvasScaler / anchor stability"));
        }

        [Test]
        public void UiSfxGuidelines_AndReportingRules_RecordHiddenUiChannelPolicy_AndScopedClaims()
        {
            var audioGuidelines = ReadRepoFile("Docs/Architecture/Audio-Architecture-Guidelines.md");
            var automationGuide = ReadRepoFile("Docs/Testing/Gameplay-Test-Automation-Guide.md");

            Assert.That(audioGuidelines, Does.Contain("UI SFX v1 Hidden Ui-Channel Sfx-Setting Policy And Ownership Matrix"));
            Assert.That(audioGuidelines, Does.Contain("UI SFX authored/routing category는 `AudioCategory.Ui`다. `AudioCategory.Sfx`로 바꾸지 않는다."));
            Assert.That(audioGuidelines, Does.Contain("effective UI SFX mix는 `Master` volume/mute, `Sfx` volume/mute, hidden `Ui` volume/mute를 모두 반영한다."));
            Assert.That(audioGuidelines, Does.Contain("`Bgm` volume/mute는 UI SFX에 영향을 주지 않는다."));
            Assert.That(audioGuidelines, Does.Contain("`Voice`와 `Ambience`는 `Sfx` volume/mute에 종속되지 않는다."));
            Assert.That(audioGuidelines, Does.Contain("public `Ui` slider 또는 mute를 Settings에 노출하는 것은 separate future product decision이다."));
            Assert.That(audioGuidelines, Does.Contain("mechanical lifecycle signal이다. direct audio trigger가 아니다."));
            Assert.That(audioGuidelines, Does.Contain("one interaction may contain multiple raw lifecycle deltas but still emit only one cue"));
            Assert.That(audioGuidelines, Does.Contain("canonical classifier matrix"));
            Assert.That(audioGuidelines, Does.Contain("classification prefers user intent over raw delta count or event ordering"));
            Assert.That(audioGuidelines, Does.Contain("canonical local-vs-flow ownership truth-source table"));
            Assert.That(audioGuidelines, Does.Contain("PausePopup.SettingsRequested"));
            Assert.That(audioGuidelines, Does.Not.Contain("reward acknowledge"));
            Assert.That(audioGuidelines, Does.Not.Contain("PausePopup.ObjectiveRequested"));
            Assert.That(audioGuidelines, Does.Contain("Display Apply"));
            Assert.That(audioGuidelines, Does.Contain("Display Revert"));
            Assert.That(audioGuidelines, Does.Contain("Settings.Back` from pause origin"));
            Assert.That(audioGuidelines, Does.Contain("`SystemPresentation` emits only explicit result-screen whitelist cues"));
            Assert.That(audioGuidelines, Does.Contain("Stage clear -> StageResult"));
            Assert.That(audioGuidelines, Does.Contain("transition overlay cue"));
            Assert.That(audioGuidelines, Does.Contain("DeathRetryChanceLost` emits `ChanceLoss` from the transition overlay path"));
            Assert.That(audioGuidelines, Does.Contain("GameClear/StageClear may share one clip through separate definitions"));
            Assert.That(audioGuidelines, Does.Contain("Death retry chance loss"));
            Assert.That(audioGuidelines, Does.Contain("LevelFailed/RetryFailed may share one clip through separate definitions"));
            Assert.That(audioGuidelines, Does.Contain("placeholder `Ui` definitions/clips는 wiring과 architecture validation 용도로 허용된다."));
            Assert.That(audioGuidelines, Does.Contain("placeholder clip reuse may make distinct cues sound similar"));
            Assert.That(audioGuidelines, Does.Contain("hover, disabled/no-op, backdrop-consume feedback는 v1 shipped scope가 아니다."));
            Assert.That(audioGuidelines, Does.Not.Contain("hidden `Ui` channel은 `Master`를 따른다. `Sfx` mute/volume을 따라가지 않는다."));
            Assert.That(audioGuidelines, Does.Not.Contain("`Sfx`를 mute해도 UI feedback은 계속 들릴 수 있다."));
            Assert.That(audioGuidelines, Does.Not.Contain("flow success cue: `UIFlowCoordinator`가 `ScreenTransitioned`, `PopupOpened`, `PopupCompleted` lifecycle signal에서만 재생한다."));
            Assert.That(audioGuidelines, Does.Not.Contain("controller lifecycle signal은 UI SFX trigger seam이다"));
            Assert.That(automationGuide, Does.Contain("UI SFX verification wording"));
            Assert.That(automationGuide, Does.Contain("hidden `Ui` authored channel, `Sfx` setting-dependent effective mix policy"));
            Assert.That(automationGuide, Does.Contain("hidden-`Ui` authored-channel policy, `Sfx` setting-dependent effective mix"));
            Assert.That(automationGuide, Does.Contain("build verified"));
            Assert.That(automationGuide, Does.Contain("ui lane validated"));
            Assert.That(automationGuide, Does.Contain("targeted UI SFX architecture validated"));
            Assert.That(automationGuide, Does.Contain("Placeholder `Ui` asset authoring is wiring evidence only"));
        }

        [Test]
        public void UiPlayModeAdditions_MustDeclareEscalationTrigger()
        {
            var playModeDirectory = GetRepoPath("Assets/_Features/UI/UI_Tests/PlayMode");
            if (!Directory.Exists(playModeDirectory))
            {
                Assert.Pass();
            }

            foreach (var filePath in Directory.GetFiles(playModeDirectory, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(filePath);
                Assert.That(content, Does.Contain("Escalation Trigger:"), filePath);
            }
        }

        private static string GetRepoPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetRepoPath(relativePath));
        }

        private static string ExtractMarkdownSection(string content, string heading)
        {
            var headingLine = heading + "\n";
            var start = content.StartsWith(headingLine, System.StringComparison.Ordinal)
                ? 0
                : content.IndexOf("\n" + headingLine, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing markdown section {heading}.");
            if (start > 0)
            {
                start += 1;
            }

            var nextHeading = content.IndexOf("\n## ", start + heading.Length, System.StringComparison.Ordinal);
            return nextHeading < 0
                ? content.Substring(start)
                : content.Substring(start, nextHeading - start);
        }

        private static void AssertDemoStageControlStalePolicyPhrasesAreAbsent(string content)
        {
            Assert.That(content, Does.Not.Contain("Needs Migration / dev-only policy"));
            Assert.That(content, Does.Not.Contain("dev-only popup"));
            Assert.That(content, Does.Not.Contain("production-disabled candidate"));
            Assert.That(content, Does.Not.Contain("DEVELOPMENT_BUILD gate candidate"));
            Assert.That(content, Does.Not.Contain("factory/runtime/hotkey/dev-path"));
        }
    }
}
