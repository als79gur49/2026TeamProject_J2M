using System;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayUiPrefabMigrationTests
    {
        private const string RootShellResourcePath = "UI/GameplayUiCanvasRootShell";

        [Test]
        public void CanonicalRootShellPrefab_HasOnlyAllowedInfrastructureChildren_AndNoFeatureViews()
        {
            var rootShellPrefab = Resources.Load<GameObject>(RootShellResourcePath);

            Assert.That(rootShellPrefab, Is.Not.Null);
            Assert.That(rootShellPrefab.GetComponent<GameplayUiCanvasRootView>(), Is.Not.Null);
            Assert.That(
                rootShellPrefab.transform.Cast<Transform>().Select(child => child.name).ToArray(),
                Is.EqualTo(new[] { "HudLayer", "ScreenLayer", "PopupLayer", "DiagnosticsLayer" }));

            var forbiddenTypes = new[]
            {
                typeof(HUDRootView),
                typeof(PlayerStatusView),
                typeof(ActionBarView),
                typeof(NotificationView),
                typeof(PausePopupView),
                typeof(ObjectiveInfoPopupView),
                typeof(ConfirmPopupView),
                typeof(TooltipPopupView),
                typeof(RewardPopupView),
                typeof(GameplayScreenView),
                typeof(HelpScreenView),
                typeof(ObjectiveStatusScreenView),
                typeof(InventoryScreenView),
                typeof(SettingsScreenView),
                typeof(StageResultScreenView),
                typeof(InventoryCatalogView),
                typeof(InventoryDetailView),
                typeof(InventoryActionView),
            };

            var foundForbiddenComponents = rootShellPrefab
                .GetComponentsInChildren<Component>(true)
                .Where(component => component != null && forbiddenTypes.Contains(component.GetType()))
                .Select(component => component.GetType().Name)
                .Distinct()
                .ToArray();

            Assert.That(foundForbiddenComponents, Is.Empty);
        }

        [Test]
        public void GameplayUiCanvasRootView_EnsureHierarchy_CreatesShellInfrastructure_ButNotHudFeatureContent()
        {
            var rootShellPrefab = Resources.Load<GameObject>(RootShellResourcePath);
            var rootShellInstance = UnityEngine.Object.Instantiate(rootShellPrefab);

            try
            {
                var rootView = rootShellInstance.GetComponent<GameplayUiCanvasRootView>();
                rootView.EnsureHierarchy();

                Assert.That(rootView.ScreenLayerView, Is.Not.Null);
                Assert.That(rootView.PopupLayerView, Is.Not.Null);
                Assert.That(rootShellInstance.GetComponentInChildren<UiArchitectureDiagnosticsOverlayView>(true), Is.Not.Null);
                Assert.That(rootView.HudView, Is.Null);
                Assert.That(rootShellInstance.GetComponentInChildren<HUDRootView>(true), Is.Null);
            }
            finally
            {
                DestroySupportObjects(rootShellInstance);
            }
        }

        [Test]
        public void GameplayUiCanvasRootView_NoLongerDefinesLegacyHudBuilderMethods()
        {
            var privateMethodNames = typeof(GameplayUiCanvasRootView)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(privateMethodNames, Does.Not.Contain("CreateHudView"));
            Assert.That(privateMethodNames, Does.Not.Contain("CreatePlayerStatusView"));
            Assert.That(privateMethodNames, Does.Not.Contain("CreateActionBarView"));
            Assert.That(privateMethodNames, Does.Not.Contain("CreateNotificationView"));
        }

        [Test]
        public void GameplayUiFlowInstaller_InstantiatesCanonicalRootShell_AndMountsAllowlistedLegacyHudBridge()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_InstantiatesCanonicalRootShell_AndMountsAllowlistedLegacyHudBridge");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                installer.Install(UiTestPortFactory.CreatePorts());

                Assert.That(installer.RootView, Is.Not.Null);
                Assert.That(installer.RootView.name, Is.EqualTo("GameplayUiCanvasRoot"));
                Assert.That(
                    installer.RootView.transform.Cast<Transform>().Select(child => child.name).ToArray(),
                    Is.EqualTo(new[] { "HudLayer", "ScreenLayer", "PopupLayer", "DiagnosticsLayer" }));
                Assert.That(installer.HudView, Is.Not.Null);
                Assert.That(installer.HudView.transform.parent, Is.EqualTo(installer.RootView.transform.Find("HudLayer")));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void UiPrefabMigrationInventory_Allowlist_IsExplicit_AndRootShellRemainsMigrated()
        {
            Assert.That(ReadInventoryBoolProperty("IsRootShellMigrated"), Is.True);
            Assert.That(ReadInventoryBoolProperty("AllowsLegacyHudBuilder"), Is.True);
            Assert.That(ReadInventoryBoolProperty("AllowsLegacyInventoryScreenSections"), Is.True);

            foreach (PopupId popupId in Enum.GetValues(typeof(PopupId)))
            {
                if (popupId == PopupId.None)
                {
                    continue;
                }

                Assert.That(InvokeInventoryBooleanMethod("AllowsLegacyPopupBuilder", popupId), Is.True, popupId.ToString());
            }

            foreach (ScreenId screenId in Enum.GetValues(typeof(ScreenId)))
            {
                if (screenId == ScreenId.None)
                {
                    continue;
                }

                Assert.That(InvokeInventoryBooleanMethod("AllowsLegacyScreenBuilder", screenId), Is.True, screenId.ToString());
            }

            var documentationTokens = ReadInventoryDocumentationTokens();
            Assert.That(documentationTokens, Has.Length.EqualTo(13));
            Assert.That(documentationTokens.Any(token => token.StartsWith("RootShell:", StringComparison.Ordinal)), Is.False);
            Assert.That(
                documentationTokens,
                Is.EqualTo(new[]
                {
                    "Hud:PersistentHud -> GameplayLegacyHudViewFactory.Create",
                    "Popup:Pause -> GameplayPopupRuntimeFactory.CreatePausePopup",
                    "Popup:ObjectiveInfo -> GameplayPopupRuntimeFactory.CreateObjectiveInfoPopup",
                    "Popup:Confirm -> GameplayPopupRuntimeFactory.CreateConfirmPopup",
                    "Popup:Tooltip -> GameplayPopupRuntimeFactory.CreateTooltipPopup",
                    "Popup:Reward -> GameplayPopupRuntimeFactory.CreateRewardPopup",
                    "Screen:Gameplay -> GameplayScreenRuntimeFactory.CreateGameplayScreen",
                    "Screen:Help -> GameplayScreenRuntimeFactory.CreateHelpScreen",
                    "Screen:ObjectiveStatus -> GameplayScreenRuntimeFactory.CreateObjectiveStatusScreen",
                    "Screen:Inventory -> GameplayScreenRuntimeFactory.CreateInventoryScreen",
                    "Screen:Settings -> GameplayScreenRuntimeFactory.CreateSettingsScreen",
                    "Screen:StageResult -> GameplayScreenRuntimeFactory.CreateStageResultScreen",
                    "ScreenInternal:InventoryScreen.Sections -> InventoryScreenView authored child sections remain runtime-built",
                }));
        }

        private static bool InvokeInventoryBooleanMethod(string methodName, object argument)
        {
            var inventoryType = GetInventoryType();
            var method = inventoryType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return (bool)method.Invoke(null, new[] { argument });
        }

        private static string[] ReadInventoryDocumentationTokens()
        {
            var inventoryType = GetInventoryType();
            var property = inventoryType.GetProperty("DocumentationTokens", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return ((System.Collections.IEnumerable)property.GetValue(null))
                .Cast<object>()
                .Select(value => value.ToString())
                .ToArray();
        }

        private static bool ReadInventoryBoolProperty(string propertyName)
        {
            var inventoryType = GetInventoryType();
            var property = inventoryType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return (bool)property.GetValue(null);
        }

        private static Type GetInventoryType()
        {
            var inventoryType = typeof(GameplayUiFlowInstaller).Assembly.GetType("Game.Feature.UI.Composition.UiPrefabMigrationInventory");
            Assert.That(inventoryType, Is.Not.Null);
            return inventoryType;
        }

        private static void DestroySupportObjects(UnityEngine.Object rootObject)
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            }

            if (rootObject != null)
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }
    }
}
