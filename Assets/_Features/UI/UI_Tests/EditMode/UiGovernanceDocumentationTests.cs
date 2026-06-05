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
            Assert.That(guide, Does.Contain("TutorialScene-Manual-Runtime-Smoke-Plan.md"));
            Assert.That(guide, Does.Contain("Display-Settings-Build-Validation-Checklist.md"));
            Assert.That(guide, Does.Contain("targeted display architecture validated"));
            Assert.That(guide, Does.Contain("real-build manual display validation completed"));
            Assert.That(guide, Does.Contain("Editor-only execution is insufficient evidence for fullscreen/window correctness."));
            Assert.That(guide, Does.Contain("green on 2026-06-06 KST"));
            Assert.That(guide, Does.Contain("Windows `dotnet build Game.Feature.UI.Tests.csproj -c Debug` passed with `0` errors"));
            Assert.That(guide, Does.Contain("Unity UI EditMode `648 total / 0 failed`"));
            Assert.That(guide, Does.Contain("2차 UI canonical 보정 보고서에 기록된 UI red 사유"));
            Assert.That(guide, Does.Contain("SurfaceBeltButtonBadgeStyleProfile"));
            Assert.That(guide, Does.Contain("SurfaceBeltButtonBadgeGroupView"));
            Assert.That(guide, Does.Contain("EnemyTargetEligibilityResult"));
            Assert.That(guide, Does.Contain("PendingEnemyBlockedReaction"));
            Assert.That(guide, Does.Contain("UI deletion candidates are removed only when the product decision, current lane evidence, and baseline note update land in the same change."));
            Assert.That(guide, Does.Not.Contain("46 total / 0 failed"));
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
            Assert.That(guidelines, Does.Contain("`StageResult`, `LevelFailed`, and `GameClear` are canonical terminal result screens."));
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
            Assert.That(guidelines, Does.Contain("Do not delete `LevelFailed`, `GameClear`, `StageResult`, `Reward` popup, `Confirm` popup, `UI_Composition` adapters, UI audio/display/settings bridge code, or the `StageNavigationRequest` path"));
            Assert.That(guidelines, Does.Contain("UI diagnostics overlay was removed as an unused runtime feature after an explicit owner decision."));
            Assert.That(guidelines, Does.Contain("Canonical runtime UI must not include a diagnostics overlay, `DiagnosticsLayer`, or F3/F4 diagnostics input path."));
            Assert.That(guidelines, Does.Not.Contain("Diagnostics overlay is also not a deletion-safe item in this phase."));
            Assert.That(guidelines, Does.Contain("This deletion decision does not change Push/Flip readiness mapping or gameplay command ownership."));
            Assert.That(
                guidelines,
                Does.Contain("PausePopup completion semantics are coordinator-owned: Resumed and Closed are resume-equivalent exits, while SettingsRequested and ObjectiveRequested keep gameplay paused, open their destination screen, and return back to a fresh PausePopup."));
            Assert.That(guidelines, Does.Not.Contain("screen-specific presenters, viewmodels, views, and screen composition"));
            Assert.That(guidelines, Does.Not.Contain("popup-specific presenters, viewmodels, views, and popup composition"));
            Assert.That(guidelines, Does.Not.Contain("persistent HUD-specific presenters, viewmodels, views, and HUD composition"));
        }

        [Test]
        public void UiBaselineNote_RecordsPhaseOneCanonicalDriftCorrections_AndDeletionProtections()
        {
            var baseline = ReadRepoFile("Docs/Testing/UI-EditMode-Baseline-2026-04-15.md");

            Assert.That(baseline, Does.Contain("Current Phase 1 drift-correction rerun: green on 2026-06-06 KST"));
            Assert.That(baseline, Does.Contain("Current Windows build result: `dotnet build Game.Feature.UI.Tests.csproj -c Debug` passed with `0` errors"));
            Assert.That(baseline, Does.Contain("Current Unity UI EditMode: `648 total / 0 failed`"));
            Assert.That(baseline, Does.Contain("Prior 2차 UI canonical correction report red reason"));
            Assert.That(baseline, Does.Contain("SurfaceBeltButtonBadgeStyleProfile"));
            Assert.That(baseline, Does.Contain("SurfaceBeltButtonBadgeGroupView"));
            Assert.That(baseline, Does.Contain("EnemyTargetEligibilityResult"));
            Assert.That(baseline, Does.Contain("PendingEnemyBlockedReaction"));
            Assert.That(baseline, Does.Contain("retired HUD proof residue was removed after product option B was selected"));
            Assert.That(baseline, Does.Contain("Help is also not a current gameplay screen"));
            Assert.That(baseline, Does.Contain("current canonical `ScreenId` values are `None`, `Gameplay`, `ObjectiveStatus`, `Settings`, `StageResult`, `LevelFailed`, and `GameClear`"));
            Assert.That(baseline, Does.Contain("`StageResultScreen`, `LevelFailedScreen`, and `GameClearScreen` remain runtime-owned terminal result screens"));
            Assert.That(baseline, Does.Contain("current canonical `PopupId` values are `None`, `Pause`, `ObjectiveInfo`, `Confirm`, `Tooltip`, `Reward`, and `DemoStageControl`"));
            Assert.That(baseline, Does.Contain("`DemoStageControl` is a catalog-less runtime assist popup created through the factory/runtime/hotkey path and not a gameplay popup catalog entry"));
            Assert.That(baseline, Does.Contain("`DemoStageControl` is a build-included tester/demo/showcase assist feature"));
            Assert.That(baseline, Does.Contain("tester assist clear, hard-section bypass, showcase navigation, and stage browsing"));
            Assert.That(baseline, Does.Contain("it is not a deletion candidate or dev-only compile exclusion target"));
            Assert.That(baseline, Does.Contain("future public-release hiding or disabling for `DemoStageControl` requires a separate product/build configuration decision"));
            Assert.That(baseline, Does.Contain("not a simple `DEVELOPMENT_BUILD` or `UNITY_EDITOR` compile gate"));
            Assert.That(baseline, Does.Contain("`Reward` and `Confirm` remain protected canonical popup paths"));
            Assert.That(baseline, Does.Contain("canonical HUD composition is `Pause`, `StageInfo`, `ObjectiveHud`, `ChancePanel`, `SurfaceBeltIndicator`, and `PlayerStatus`"));
            Assert.That(baseline, Does.Contain("UI diagnostics overlay was removed as an unused runtime feature; it is not hidden, dev-only retained, or a protected runtime path"));
            Assert.That(baseline, Does.Contain("protected UI paths for drift correction include `LevelFailed`, `GameClear`, `StageResult`, `Reward` popup, `Confirm` popup, `UI_Composition` adapters, UI audio/display/settings bridges, and `StageNavigationRequest`"));
            Assert.That(baseline, Does.Not.Contain("diagnostics overlay pending a separate production/dev-only policy decision"));
            AssertDemoStageControlStalePolicyPhrasesAreAbsent(baseline);
            Assert.That(baseline, Does.Not.Contain("HelpScreen remains"));
            Assert.That(baseline, Does.Not.Contain("InventoryScreen remains"));
        }

        [Test]
        public void TutorialSceneManualRuntimeSmokePlan_PreservesBoundedArchitectureFocusedSections()
        {
            var smokePlan = ReadRepoFile("Docs/Testing/TutorialScene-Manual-Runtime-Smoke-Plan.md");

            Assert.That(smokePlan, Does.Contain("# TutorialScene Manual Runtime Smoke Plan"));
            Assert.That(smokePlan, Does.Contain("## 1. Overall Evaluation"));
            Assert.That(smokePlan, Does.Contain("## 2. Preserved Strengths"));
            Assert.That(smokePlan, Does.Contain("## 3. Remaining Execution Risks"));
            Assert.That(smokePlan, Does.Contain("## 4. Required Corrections"));
            Assert.That(smokePlan, Does.Contain("## 5. High-Risk Runtime Flow Rules"));
            Assert.That(smokePlan, Does.Contain("## 6. Tooltip Path Classification Rules"));
            Assert.That(smokePlan, Does.Contain("## 7. Stage-Clear Validation Rules"));
            Assert.That(smokePlan, Does.Contain("## 8. Removed Diagnostics Overlay Rules"));
            Assert.That(smokePlan, Does.Contain("## 9. Evidence and Failure Classification Rules"));
            Assert.That(smokePlan, Does.Contain("## 10. Freeze Gate"));
            Assert.That(smokePlan, Does.Contain("Tier 1"));
            Assert.That(smokePlan, Does.Contain("ScreenId.Gameplay"));
            Assert.That(smokePlan, Does.Contain("PausePopup"));
            Assert.That(smokePlan, Does.Contain("SettingsScreen"));
            Assert.That(smokePlan, Does.Contain("StageResultScreen"));
            Assert.That(smokePlan, Does.Contain("TooltipPopup"));
            Assert.That(smokePlan, Does.Contain("SettingsScreen tooltip info icon"));
            Assert.That(smokePlan, Does.Contain("future tooltip expansion requires separate plan/review"));
            Assert.That(smokePlan, Does.Contain("Inconclusive/manual follow-up needed"));
            Assert.That(smokePlan, Does.Contain("up to 3 deliberate attempts"));
            Assert.That(smokePlan, Does.Contain("up to 10 focused minutes"));
            Assert.That(smokePlan, Does.Contain("Diagnostics overlay is a removed unused runtime feature"));
            Assert.That(smokePlan, Does.Contain("Manual smoke should not attempt F3/F4 diagnostics overlay interaction."));
            Assert.That(smokePlan, Does.Contain("architecture-focused"));
            Assert.That(smokePlan, Does.Contain("Do not add scene-local helpers"));
            Assert.That(smokePlan, Does.Contain("artificial debug triggers"));
            Assert.That(smokePlan, Does.Contain("editor-only execution is insufficient evidence"));
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
                Does.Contain("The Settings resolution hover hint is a local SettingsDisplaySection affordance, remains available regardless of the Tooltips accessibility toggle, and does not use TooltipPopup or popup flow."));
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

            Assert.That(audioGuidelines, Does.Contain("UI SFX v1 Hidden Ui-Channel Policy And Ownership Matrix"));
            Assert.That(audioGuidelines, Does.Contain("hidden `Ui` channel은 `Master`를 따른다. `Sfx` mute/volume을 따라가지 않는다."));
            Assert.That(audioGuidelines, Does.Contain("public `Ui` slider 또는 mute를 Settings에 노출하는 것은 separate future product decision이다."));
            Assert.That(audioGuidelines, Does.Contain("mechanical lifecycle signal이다. direct audio trigger가 아니다."));
            Assert.That(audioGuidelines, Does.Contain("one interaction may contain multiple raw lifecycle deltas but still emit only one cue"));
            Assert.That(audioGuidelines, Does.Contain("canonical classifier matrix"));
            Assert.That(audioGuidelines, Does.Contain("classification prefers user intent over raw delta count or event ordering"));
            Assert.That(audioGuidelines, Does.Contain("canonical local-vs-flow ownership truth-source table"));
            Assert.That(audioGuidelines, Does.Contain("PausePopup.SettingsRequested"));
            Assert.That(audioGuidelines, Does.Contain("PausePopup.ObjectiveRequested"));
            Assert.That(audioGuidelines, Does.Contain("Display Apply"));
            Assert.That(audioGuidelines, Does.Contain("Display Revert"));
            Assert.That(audioGuidelines, Does.Contain("Settings.Back` from pause origin"));
            Assert.That(audioGuidelines, Does.Contain("`SystemPresentation` emits only explicit result-screen whitelist cues"));
            Assert.That(audioGuidelines, Does.Contain("Stage clear -> StageResult + Reward popup"));
            Assert.That(audioGuidelines, Does.Contain("transition overlay cue"));
            Assert.That(audioGuidelines, Does.Contain("DeathRetryChanceLost` emits `ChanceLoss` from the transition overlay path"));
            Assert.That(audioGuidelines, Does.Contain("GameClear/StageClear may share one clip through separate definitions"));
            Assert.That(audioGuidelines, Does.Contain("Death retry chance loss"));
            Assert.That(audioGuidelines, Does.Contain("LevelFailed/RetryFailed may share one clip through separate definitions"));
            Assert.That(audioGuidelines, Does.Contain("placeholder `Ui` definitions/clips는 wiring과 architecture validation 용도로 허용된다."));
            Assert.That(audioGuidelines, Does.Contain("placeholder clip reuse may make distinct cues sound similar"));
            Assert.That(audioGuidelines, Does.Contain("hover, disabled/no-op, backdrop-consume feedback는 v1 shipped scope가 아니다."));
            Assert.That(audioGuidelines, Does.Not.Contain("flow success cue: `UIFlowCoordinator`가 `ScreenTransitioned`, `PopupOpened`, `PopupCompleted` lifecycle signal에서만 재생한다."));
            Assert.That(audioGuidelines, Does.Not.Contain("controller lifecycle signal은 UI SFX trigger seam이다"));
            Assert.That(automationGuide, Does.Contain("UI SFX verification wording"));
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
