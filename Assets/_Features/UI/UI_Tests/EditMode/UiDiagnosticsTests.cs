using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class UiDiagnosticsTests
    {
        [Test]
        public void GameplayUiFlowInstaller_DiagnosticsOverlay_TogglesRemainReadOnly()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_DiagnosticsOverlay_TogglesRemainReadOnly");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts());

                var overlay = rootObject.GetComponentInChildren<UiArchitectureDiagnosticsOverlayView>(true);
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.IsSupported, Is.True);

                var beforeSnapshot = installer.PresentationSource.CurrentSnapshot;
                var beforeBlockSnapshot = installer.Coordinator.CurrentBlockSnapshot;
                var beforeScreenId = installer.ScreenController.CurrentScreenId;
                var beforeBackStackCount = installer.ScreenController.BackStackCount;
                var beforePopupCount = installer.PopupController.PopupCount;
                var beforeHudInteractive = installer.HudController.ActionBarViewModel.IsInteractive;

                overlay.ToggleVisibility();
                overlay.ToggleExpanded();

                Assert.That(installer.PresentationSource.CurrentSnapshot, Is.EqualTo(beforeSnapshot));
                Assert.That(installer.Coordinator.CurrentBlockSnapshot, Is.EqualTo(beforeBlockSnapshot));
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(beforeScreenId));
                Assert.That(installer.ScreenController.BackStackCount, Is.EqualTo(beforeBackStackCount));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(beforePopupCount));
                Assert.That(installer.HudController.ActionBarViewModel.IsInteractive, Is.EqualTo(beforeHudInteractive));
                Assert.That(overlay.IsOverlayVisible, Is.True);
                Assert.That(overlay.IsExpanded, Is.True);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_DiagnosticsOverlay_SummaryIsBoundedAndDetailsAreOptIn()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_DiagnosticsOverlay_SummaryIsBoundedAndDetailsAreOptIn");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts());
                var overlay = rootObject.GetComponentInChildren<UiArchitectureDiagnosticsOverlayView>(true);

                Assert.That(overlay, Is.Not.Null);
                overlay.ToggleVisibility();

                Assert.That(overlay.IsOverlayVisible, Is.True);
                Assert.That(overlay.IsDetailsVisible, Is.False);
                Assert.That(overlay.SummaryText, Does.Contain("Screen:"));
                Assert.That(overlay.SummaryText, Does.Contain("Back Stack:"));
                Assert.That(overlay.SummaryText, Does.Contain("Popup:"));
                Assert.That(overlay.SummaryText, Does.Contain("Popup Depth:"));
                Assert.That(overlay.SummaryText, Does.Contain("Dismissibility:"));
                Assert.That(overlay.SummaryText, Does.Contain("HUD:"));
                Assert.That(overlay.SummaryText, Does.Contain("Block Source:"));
                Assert.That(overlay.SummaryText, Does.Contain("Mapped Tick:"));
                Assert.That(overlay.SummaryText, Does.Contain("Latest Event:"));
                Assert.That(overlay.SummaryText, Does.Not.Contain("Screen Instance:"));
                Assert.That(overlay.SummaryText, Does.Not.Contain("Popup Policy:"));
                Assert.That(overlay.SummaryText, Does.Not.Contain("Recent Events:"));
                Assert.That(overlay.SummaryText, Does.Not.Contain("Inventory Summary:"));

                installer.GameplayScreenView.ClickInventory();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Inventory));

                overlay.ToggleExpanded();

                Assert.That(overlay.IsDetailsVisible, Is.True);
                Assert.That(overlay.DetailText, Does.Contain("Screen Instance:"));
                Assert.That(overlay.DetailText, Does.Contain("Top Popup Instance:"));
                Assert.That(overlay.DetailText, Does.Contain("Popup Policy:"));
                Assert.That(overlay.DetailText, Does.Contain("Recent Events:"));
                Assert.That(overlay.DetailText, Does.Contain("Inventory Summary:"));
                Assert.That(overlay.DetailText, Does.Contain("Detail=Crystal Shard"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        private static void DestroySupportObjects(GameObject rootObject)
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }

            if (rootObject != null)
            {
                Object.DestroyImmediate(rootObject);
            }
        }
    }
}
