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
            Assert.That(baseline, Does.Contain("## Prefab Migration Mixed-Mode Inventory"));
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
            Assert.That(baseline, Does.Contain("root shell, HUD, popup, and screen migration allowlists are now empty"));
            Assert.That(baseline, Does.Not.Contain("Hud:PersistentHud -> GameplayLegacyHudViewFactory.Create"));
            Assert.That(baseline, Does.Contain("HUD legacy runtime builder path was removed in the same phase"));
            Assert.That(baseline, Does.Contain("bounded HUD proof"));
            Assert.That(baseline, Does.Contain("popup legacy runtime builder paths were removed in the same phase"));
            Assert.That(baseline, Does.Contain("popup catalog remains fixed-shape and popup-only"));
            Assert.That(baseline, Does.Contain("must not be treated as precedent for screen migration"));
            Assert.That(baseline, Does.Contain("screen legacy runtime builder paths were removed in the same phase"));
            Assert.That(baseline, Does.Contain("screen catalog remains fixed-shape and screen-only"));
            Assert.That(baseline, Does.Contain("screen hybrid allowlist is now empty"));
            Assert.That(baseline, Does.Contain("simple-shell checkpoint"));
            Assert.That(baseline, Does.Contain("terminal-screen checkpoint"));
            Assert.That(baseline, Does.Contain("complex-screen checkpoint"));
            Assert.That(baseline, Does.Not.Contain("simple-shell checkpoint is complete for `Help`, `ObjectiveStatus`, and `Settings`"));
            Assert.That(baseline, Does.Contain("simple-shell checkpoint is complete for `Help` and `ObjectiveStatus`"));
            Assert.That(baseline, Does.Contain("complex-screen checkpoint is complete for `Inventory` and bounded `Settings`"));
            Assert.That(baseline, Does.Contain("Settings authored child-view canonicalization is closed here"));
            Assert.That(baseline, Does.Contain("`UiPrefabMigrationInventory` removal and unrelated migration/helper cleanup remain later work"));
            Assert.That(baseline, Does.Not.Contain("Popup:Pause -> GameplayPopupRuntimeFactory.CreatePausePopup"));
            Assert.That(baseline, Does.Not.Contain("ScreenInternal:InventoryScreen.Sections -> InventoryScreenView authored child sections remain runtime-built"));
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
            Assert.That(
                guidelines,
                Does.Contain("PausePopup completion semantics are coordinator-owned: Resumed and Closed are resume-equivalent exits, while SettingsRequested keeps gameplay paused, opens SettingsScreen, and returns back to a fresh PausePopup."));
            Assert.That(guidelines, Does.Not.Contain("screen-specific presenters, viewmodels, views, and screen composition"));
            Assert.That(guidelines, Does.Not.Contain("popup-specific presenters, viewmodels, views, and popup composition"));
            Assert.That(guidelines, Does.Not.Contain("persistent HUD-specific presenters, viewmodels, views, and HUD composition"));
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
            Assert.That(smokePlan, Does.Contain("## 8. Diagnostics Priority Rules"));
            Assert.That(smokePlan, Does.Contain("## 9. Evidence and Failure Classification Rules"));
            Assert.That(smokePlan, Does.Contain("## 10. Freeze Gate"));
            Assert.That(smokePlan, Does.Contain("Tier 1"));
            Assert.That(smokePlan, Does.Contain("GameplayScreen"));
            Assert.That(smokePlan, Does.Contain("PausePopup"));
            Assert.That(smokePlan, Does.Contain("InventoryScreen"));
            Assert.That(smokePlan, Does.Contain("StageResultScreen"));
            Assert.That(smokePlan, Does.Contain("TooltipPopup"));
            Assert.That(smokePlan, Does.Contain("SettingsScreen tooltip info icon"));
            Assert.That(smokePlan, Does.Contain("future tooltip expansion requires separate plan/review"));
            Assert.That(smokePlan, Does.Contain("Inconclusive/manual follow-up needed"));
            Assert.That(smokePlan, Does.Contain("up to 3 deliberate attempts"));
            Assert.That(smokePlan, Does.Contain("up to 10 focused minutes"));
            Assert.That(smokePlan, Does.Contain("Diagnostics remain secondary"));
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
            Assert.That(displayGuidelines, Does.Contain("15-second timeout"));
            Assert.That(displayGuidelines, Does.Contain("Do not add a scene-global fallback lookup."));
            Assert.That(displayGuidelines, Does.Contain("## Settings Authored Child-View Checklist"));
            Assert.That(displayGuidelines, Does.Contain("SettingsAudioSection"));
            Assert.That(displayGuidelines, Does.Contain("SettingsDisplaySection"));
            Assert.That(displayGuidelines, Does.Contain("SettingsScreenView` must serialize `_audioView` and `_displayView` directly"));
            Assert.That(
                displayGuidelines,
                Does.Contain("The Settings resolution hover hint is a local SettingsDisplaySection affordance, remains available regardless of the Tooltips accessibility toggle, and does not use TooltipPopup or popup flow."));
            Assert.That(displayGuidelines, Does.Not.Contain("TooltipPopup auto-hide"));
            Assert.That(displayGuidelines, Does.Contain("Settings authored child-view canonicalization only"));
            Assert.That(displayGuidelines, Does.Contain("UiPrefabMigrationInventory` cleanup"));
            Assert.That(buildChecklist, Does.Contain("Editor-only execution is insufficient evidence for fullscreen/window correctness."));
            Assert.That(buildChecklist, Does.Contain("startup apply"));
            Assert.That(buildChecklist, Does.Contain("timeout revert"));
            Assert.That(buildChecklist, Does.Contain("alt-tab"));
            Assert.That(buildChecklist, Does.Contain("CanvasScaler / anchor stability"));
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
    }
}
