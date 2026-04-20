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
            Assert.That(guide, Does.Not.Contain("46 total / 0 failed"));
        }

        [Test]
        public void TutorialSceneManualRuntimeSmokePlan_PreservesBoundedArchitectureFocusedSections()
        {
            var smokePlan = ReadRepoFile("Docs/Testing/TutorialScene-Manual-Runtime-Smoke-Plan.md");
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
            Assert.That(guidelines, Does.Not.Contain("screen-specific presenters, viewmodels, views, and screen composition"));
            Assert.That(guidelines, Does.Not.Contain("popup-specific presenters, viewmodels, views, and popup composition"));
            Assert.That(guidelines, Does.Not.Contain("persistent HUD-specific presenters, viewmodels, views, and HUD composition"));
        }


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
