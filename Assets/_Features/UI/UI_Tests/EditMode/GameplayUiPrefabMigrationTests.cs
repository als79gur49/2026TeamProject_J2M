using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayUiPrefabMigrationTests
    {
        private const string RootShellResourcePath = "UI/GameplayUiCanvasRootShell";
        private const string InstallerSourcePath = "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs";
        private const string ScreenFactorySourcePath = "Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs";
        private const string PopupFactorySourcePath = "Assets/_Features/UI/UI_Composition/Runtime/GameplayPopupRuntimeFactory.cs";
        private const string PausePopupViewSourcePath = "Assets/_Features/UI/UI_Popups/Runtime/PausePopupView.cs";
        private const string ObjectiveInfoPopupViewSourcePath = "Assets/_Features/UI/UI_Popups/Runtime/ObjectiveInfoPopupView.cs";
        private const string ConfirmPopupViewSourcePath = "Assets/_Features/UI/UI_Popups/Runtime/ConfirmPopupView.cs";
        private const string TooltipPopupViewSourcePath = "Assets/_Features/UI/UI_Popups/Runtime/TooltipPopupView.cs";
        private const string RewardPopupViewSourcePath = "Assets/_Features/UI/UI_Popups/Runtime/RewardPopupView.cs";
        private const string BaselineNotePath = "Docs/Testing/UI-EditMode-Baseline-2026-04-15.md";

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
                typeof(SettingsAudioView),
                typeof(SettingsDisplayView),
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
        public void CanonicalRootShellPrefab_HudLayerStartsEmpty_AndContainsNoShellOwnedHudMarkup()
        {
            var rootShellPrefab = Resources.Load<GameObject>(RootShellResourcePath);
            Assert.That(rootShellPrefab, Is.Not.Null);

            var hudLayer = rootShellPrefab.transform.Find("HudLayer");
            Assert.That(hudLayer, Is.Not.Null);
            Assert.That(hudLayer.childCount, Is.Zero);
            Assert.That(rootShellPrefab.GetComponentsInChildren<Button>(true), Is.Empty);
            Assert.That(rootShellPrefab.GetComponentsInChildren<Text>(true), Is.Empty);
            Assert.That(rootShellPrefab.GetComponentsInChildren<CanvasGroup>(true), Is.Empty);
        }

        [Test]
        public void CanonicalHudPrefabAsset_UsesAuthoredChildViews_AndNoCrossLayerOwners()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();

            Assert.That(hudPrefab, Is.Not.Null);
            Assert.That(hudPrefab.PlayerStatusView, Is.Not.Null);
            Assert.That(hudPrefab.ActionBarView, Is.Not.Null);
            Assert.That(hudPrefab.NotificationView, Is.Not.Null);
            Assert.That(hudPrefab.GetComponentsInChildren<GameplayUiCanvasRootView>(true), Is.Empty);
            Assert.That(hudPrefab.GetComponentsInChildren<ScreenLayerView>(true), Is.Empty);
            Assert.That(hudPrefab.GetComponentsInChildren<PopupLayerView>(true), Is.Empty);
            Assert.That(hudPrefab.GetComponentsInChildren<PausePopupView>(true), Is.Empty);
            Assert.That(hudPrefab.GetComponentsInChildren<GameplayScreenView>(true), Is.Empty);
        }

        [Test]
        public void PopupPrefabCatalog_PublicAndSerializedSurface_RemainsFixedShapePopupOnly()
        {
            var popupCatalog = UiTestPrefabAssetUtility.LoadPopupCatalog();
            var catalogType = typeof(PopupPrefabCatalog);
            var instanceFields = catalogType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsStatic)
                .ToArray();
            var publicPropertyNames = catalogType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(
                instanceFields.Select(field => field.Name).ToArray(),
                Is.EqualTo(new[]
                {
                    "_pausePrefab",
                    "_objectiveInfoPrefab",
                    "_confirmPrefab",
                    "_tooltipPrefab",
                    "_rewardPrefab",
                }));
            Assert.That(
                instanceFields.Select(field => field.FieldType).ToArray(),
                Is.EqualTo(new[]
                {
                    typeof(PausePopupView),
                    typeof(ObjectiveInfoPopupView),
                    typeof(ConfirmPopupView),
                    typeof(TooltipPopupView),
                    typeof(RewardPopupView),
                }));
            Assert.That(
                publicPropertyNames,
                Is.EqualTo(new[]
                {
                    "ConfirmPrefab",
                    "ObjectiveInfoPrefab",
                    "PausePrefab",
                    "RewardPrefab",
                    "TooltipPrefab",
                }));
            Assert.That(instanceFields.Any(field => field.FieldType == typeof(UnityEngine.Object)), Is.False);
            Assert.That(instanceFields.Any(field =>
                typeof(IEnumerable).IsAssignableFrom(field.FieldType) &&
                field.FieldType != typeof(string)), Is.False);
            Assert.That(instanceFields.Any(field => field.FieldType == typeof(string)), Is.False);
            Assert.That(catalogType.Name, Does.Not.Contain("Registry"));
            Assert.That(catalogType.Name, Does.Not.Contain("Lookup"));
            Assert.That(catalogType.Name, Does.Not.Contain("Variant"));

            Assert.That(AssetDatabase.GetAssetPath(popupCatalog.PausePrefab), Is.EqualTo(UiTestPrefabAssetUtility.PausePopupPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(popupCatalog.ObjectiveInfoPrefab), Is.EqualTo(UiTestPrefabAssetUtility.ObjectiveInfoPopupPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(popupCatalog.ConfirmPrefab), Is.EqualTo(UiTestPrefabAssetUtility.ConfirmPopupPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(popupCatalog.TooltipPrefab), Is.EqualTo(UiTestPrefabAssetUtility.TooltipPopupPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(popupCatalog.RewardPrefab), Is.EqualTo(UiTestPrefabAssetUtility.RewardPopupPrefabPath));
        }

        [Test]
        public void ScreenPrefabCatalog_PublicAndSerializedSurface_RemainsFixedShapeScreenOnly()
        {
            var screenCatalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
            var catalogType = typeof(ScreenPrefabCatalog);
            var instanceFields = catalogType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsStatic)
                .ToArray();
            var publicPropertyNames = catalogType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(
                instanceFields.Select(field => field.Name).ToArray(),
                Is.EqualTo(new[]
                {
                    "_gameplayPrefab",
                    "_helpPrefab",
                    "_objectiveStatusPrefab",
                    "_inventoryPrefab",
                    "_settingsPrefab",
                    "_stageResultPrefab",
                }));
            Assert.That(
                instanceFields.Select(field => field.FieldType).ToArray(),
                Is.EqualTo(new[]
                {
                    typeof(GameplayScreenView),
                    typeof(HelpScreenView),
                    typeof(ObjectiveStatusScreenView),
                    typeof(InventoryScreenView),
                    typeof(SettingsScreenView),
                    typeof(StageResultScreenView),
                }));
            Assert.That(
                publicPropertyNames,
                Is.EqualTo(new[]
                {
                    "GameplayPrefab",
                    "HelpPrefab",
                    "InventoryPrefab",
                    "ObjectiveStatusPrefab",
                    "SettingsPrefab",
                    "StageResultPrefab",
                }));
            Assert.That(instanceFields.Any(field => field.FieldType == typeof(UnityEngine.Object)), Is.False);
            Assert.That(instanceFields.Any(field =>
                typeof(IEnumerable).IsAssignableFrom(field.FieldType) &&
                field.FieldType != typeof(string)), Is.False);
            Assert.That(instanceFields.Any(field => field.FieldType == typeof(string)), Is.False);
            Assert.That(catalogType.Name, Does.Not.Contain("Registry"));
            Assert.That(catalogType.Name, Does.Not.Contain("Lookup"));
            Assert.That(catalogType.Name, Does.Not.Contain("Variant"));
            Assert.That(catalogType.Name, Does.Not.Contain("Theme"));

            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.GameplayPrefab), Is.EqualTo(UiTestPrefabAssetUtility.GameplayScreenPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.HelpPrefab), Is.EqualTo(UiTestPrefabAssetUtility.HelpScreenPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.ObjectiveStatusPrefab), Is.EqualTo(UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.InventoryPrefab), Is.EqualTo(UiTestPrefabAssetUtility.InventoryScreenPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.SettingsPrefab), Is.EqualTo(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.StageResultPrefab), Is.EqualTo(UiTestPrefabAssetUtility.StageResultScreenPrefabPath));
        }

        [Test]
        public void PausePopupPrefabAsset_UsesAuthoredPopupView_AndNoCrossLayerOwners()
        {
            AssertPopupPrefabContract<PausePopupView>(UiTestPrefabAssetUtility.PausePopupPrefabPath);
        }

        [Test]
        public void ObjectiveInfoPopupPrefabAsset_UsesAuthoredPopupView_AndNoCrossLayerOwners()
        {
            AssertPopupPrefabContract<ObjectiveInfoPopupView>(UiTestPrefabAssetUtility.ObjectiveInfoPopupPrefabPath);
        }

        [Test]
        public void ConfirmPopupPrefabAsset_UsesAuthoredPopupView_AndNoCrossLayerOwners()
        {
            AssertPopupPrefabContract<ConfirmPopupView>(UiTestPrefabAssetUtility.ConfirmPopupPrefabPath);
        }

        [Test]
        public void TooltipPopupPrefabAsset_UsesAuthoredPopupView_AndNoCrossLayerOwners()
        {
            AssertPopupPrefabContract<TooltipPopupView>(UiTestPrefabAssetUtility.TooltipPopupPrefabPath);
        }

        [Test]
        public void RewardPopupPrefabAsset_UsesAuthoredPopupView_AndNoCrossLayerOwners()
        {
            AssertPopupPrefabContract<RewardPopupView>(UiTestPrefabAssetUtility.RewardPopupPrefabPath);
        }

        [TestCase(ScreenId.Gameplay)]
        [TestCase(ScreenId.Help)]
        [TestCase(ScreenId.ObjectiveStatus)]
        [TestCase(ScreenId.Inventory)]
        [TestCase(ScreenId.Settings)]
        [TestCase(ScreenId.StageResult)]
        public void CanonicalScreenPrefabAsset_UsesAuthoredScreenView_AndNoCrossLayerOwners(ScreenId screenId)
        {
            switch (screenId)
            {
                case ScreenId.Gameplay:
                    AssertScreenPrefabContract<GameplayScreenView>(
                        UiTestPrefabAssetUtility.GameplayScreenPrefabPath,
                        typeof(GameplayScreenView));
                    break;

                case ScreenId.Help:
                    AssertScreenPrefabContract<HelpScreenView>(
                        UiTestPrefabAssetUtility.HelpScreenPrefabPath,
                        typeof(HelpScreenView));
                    break;

                case ScreenId.ObjectiveStatus:
                    AssertScreenPrefabContract<ObjectiveStatusScreenView>(
                        UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath,
                        typeof(ObjectiveStatusScreenView));
                    break;

                case ScreenId.Inventory:
                {
                    var inventoryPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<InventoryScreenView>(UiTestPrefabAssetUtility.InventoryScreenPrefabPath);
                    Assert.That(inventoryPrefab.CatalogView, Is.Not.Null);
                    Assert.That(inventoryPrefab.DetailView, Is.Not.Null);
                    Assert.That(inventoryPrefab.ActionView, Is.Not.Null);
                    AssertScreenPrefabHasNoCrossLayerOwners(
                        inventoryPrefab.gameObject,
                        typeof(InventoryScreenView),
                        typeof(InventoryCatalogView),
                        typeof(InventoryDetailView),
                        typeof(InventoryActionView));
                    break;
                }

                case ScreenId.Settings:
                    AssertScreenPrefabContract<SettingsScreenView>(
                        UiTestPrefabAssetUtility.SettingsScreenPrefabPath,
                        typeof(SettingsScreenView),
                        typeof(SettingsAudioView),
                        typeof(SettingsDisplayView));
                    break;

                case ScreenId.StageResult:
                    AssertScreenPrefabContract<StageResultScreenView>(
                        UiTestPrefabAssetUtility.StageResultScreenPrefabPath,
                        typeof(StageResultScreenView));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(screenId), screenId, null);
            }
        }

        [Test]
        public void SettingsScreenPrefabAsset_AuthorsTooltipInfoAffordance_AsBoundedLocalIntent()
        {
            var settingsPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var serializedView = new SerializedObject(settingsPrefab);
            var tooltipInfoButton = serializedView.FindProperty("_tooltipInfoButton");

            Assert.That(tooltipInfoButton, Is.Not.Null);
            Assert.That(tooltipInfoButton.objectReferenceValue, Is.Not.Null);
            Assert.That(((Button)tooltipInfoButton.objectReferenceValue).transform.parent, Is.EqualTo(settingsPrefab.transform));
        }

        [Test]
        public void SettingsScreenPrefabAsset_AuthorsRequiredChildSections_AndSerializedChildRefs()
        {
            var settingsPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var serializedRoot = new SerializedObject(settingsPrefab);
            var audioViewProperty = serializedRoot.FindProperty("_audioView");
            var displayViewProperty = serializedRoot.FindProperty("_displayView");

            Assert.That(audioViewProperty, Is.Not.Null);
            Assert.That(displayViewProperty, Is.Not.Null);
            Assert.That(audioViewProperty.objectReferenceValue, Is.Not.Null);
            Assert.That(displayViewProperty.objectReferenceValue, Is.Not.Null);
            Assert.That(serializedRoot.FindProperty("_mainRow"), Is.Null);
            Assert.That(serializedRoot.FindProperty("_resolutionDropdown"), Is.Null);
            Assert.That(serializedRoot.FindProperty("_applyButton"), Is.Null);

            var audioView = (SettingsAudioView)audioViewProperty.objectReferenceValue;
            var displayView = (SettingsDisplayView)displayViewProperty.objectReferenceValue;

            Assert.That(audioView.name, Is.EqualTo(SettingsScreenView.AudioSectionName));
            Assert.That(displayView.name, Is.EqualTo(SettingsScreenView.DisplaySectionName));
            Assert.That(audioView.transform.parent, Is.EqualTo(settingsPrefab.transform));
            Assert.That(displayView.transform.parent, Is.EqualTo(settingsPrefab.transform));

            var serializedAudio = new SerializedObject(audioView);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_mainRow._rowRoot", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_mainRow._label", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_mainRow._value", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_mainRow._slider", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_mainRow._toggle", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_mainRow._interactionRelay", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_bgmRow._rowRoot", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_bgmRow._label", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_bgmRow._value", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_bgmRow._slider", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_bgmRow._toggle", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_bgmRow._interactionRelay", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_sfxRow._rowRoot", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_sfxRow._label", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_sfxRow._value", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_sfxRow._slider", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_sfxRow._toggle", audioView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedAudio, "_sfxRow._interactionRelay", audioView.transform);

            var serializedDisplay = new SerializedObject(displayView);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_sectionTitle", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_currentDisplayLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_currentDisplayValue", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_resolutionLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_resolutionDropdown", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_fullscreenLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_fullscreenToggle", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_displayStatusLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_applyButton", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_applyButtonLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_revertButton", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_revertButtonLabel", displayView.transform);
        }

        [Test]
        public void ScreenPrefabMigrationAuthoring_Source_AuthorsSettingsChildSections_AndWiresSerializedChildViews()
        {
            var authoringSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Editor/ScreenPrefabMigrationAuthoring.cs");

            Assert.That(authoringSource, Does.Contain("SettingsScreenView.AudioSectionName"));
            Assert.That(authoringSource, Does.Contain("SettingsScreenView.DisplaySectionName"));
            Assert.That(authoringSource, Does.Contain("_audioView"));
            Assert.That(authoringSource, Does.Contain("_displayView"));
        }

        [TestCase(ScreenId.Gameplay)]
        [TestCase(ScreenId.Help)]
        [TestCase(ScreenId.ObjectiveStatus)]
        [TestCase(ScreenId.Inventory)]
        [TestCase(ScreenId.Settings)]
        public void CanonicalScreenFactoryPath_InstantiatesCanonicalScreenPrefabUnderScreenLayer(ScreenId screenId)
        {
            var rootObject = new GameObject($"CanonicalScreenFactoryPath_{screenId}");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts());

                var screenView = OpenMountedScreen(installer, screenId);
                var screenPrefabView = LoadScreenPrefabView(screenId);

                Assert.That(screenView, Is.Not.Null, screenId.ToString());
                Assert.That(screenPrefabView, Is.Not.Null, screenId.ToString());
                Assert.That(screenView.transform.parent, Is.EqualTo(installer.ScreenLayerView.ContentRoot), screenId.ToString());
                Assert.That(screenView, Is.Not.SameAs(screenPrefabView), screenId.ToString());
                Assert.That(screenView.GetType(), Is.EqualTo(screenPrefabView.GetType()), screenId.ToString());
                Assert.That(screenView.gameObject.name, Does.StartWith(screenPrefabView.gameObject.name), screenId.ToString());
                Assert.That(screenView.gameObject.scene.IsValid(), Is.True, screenId.ToString());
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(screenId), screenId.ToString());
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_UsesBoundedHudScreenAndPopupPrefabReferences_WithoutRegistryGrowth()
        {
            var installerType = typeof(GameplayUiFlowInstaller);
            var instanceFields = installerType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsStatic)
                .ToArray();

            Assert.That(
                instanceFields.Where(field => field.FieldType == typeof(HUDRootView)).Select(field => field.Name).ToArray(),
                Is.EqualTo(new[] { "_hudPrefab" }));
            Assert.That(
                instanceFields.Where(field => field.FieldType == typeof(ScreenPrefabCatalog)).Select(field => field.Name).ToArray(),
                Is.EqualTo(new[] { "_screenPrefabCatalog" }));
            Assert.That(
                instanceFields.Where(field => field.FieldType == typeof(PopupPrefabCatalog)).Select(field => field.Name).ToArray(),
                Is.EqualTo(new[] { "_popupPrefabCatalog" }));
            Assert.That(
                instanceFields.Where(field => field.Name.Contains("Catalog", StringComparison.OrdinalIgnoreCase))
                    .Select(field => field.Name)
                    .OrderBy(name => name)
                    .ToArray(),
                Is.EqualTo(new[] { "_popupPrefabCatalog", "_screenPrefabCatalog" }));
            Assert.That(instanceFields.Any(field => field.Name.Contains("Registry", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(instanceFields.Any(field => field.Name.Contains("Lookup", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(typeof(GameplayUiFlowInstaller).Assembly.GetTypes().Select(type => type.Name), Has.No.Member("HudPrefabRegistry"));
            Assert.That(typeof(GameplayUiFlowInstaller).Assembly.GetTypes().Select(type => type.Name), Has.No.Member("ScreenPrefabRegistry"));
            Assert.That(typeof(GameplayUiFlowInstaller).Assembly.GetTypes().Select(type => type.Name), Has.No.Member("PopupPrefabRegistry"));
            Assert.That(typeof(GameplayUiFlowInstaller).Assembly.GetTypes().Select(type => type.Name), Has.No.Member("UiAssetRegistry"));
        }

        [Test]
        public void GameplayUiFlowInstaller_InstantiatesCanonicalRootShell_AndMountsCanonicalHudPrefab()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_InstantiatesCanonicalRootShell_AndMountsCanonicalHudPrefab");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts());

                Assert.That(installer.RootView, Is.Not.Null);
                Assert.That(installer.RootView.name, Is.EqualTo("GameplayUiCanvasRoot"));
                Assert.That(
                    installer.RootView.transform.Cast<Transform>().Select(child => child.name).ToArray(),
                    Is.EqualTo(new[] { "HudLayer", "ScreenLayer", "PopupLayer", "DiagnosticsLayer" }));
                Assert.That(installer.HudView, Is.Not.Null);
                Assert.That(installer.HudView.transform.parent, Is.EqualTo(installer.RootView.transform.Find("HudLayer")));
                Assert.That(installer.HudView, Is.Not.SameAs(UiTestPrefabAssetUtility.LoadHudPrefab()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [TestCase(PopupId.Pause)]
        [TestCase(PopupId.ObjectiveInfo)]
        [TestCase(PopupId.Confirm)]
        [TestCase(PopupId.Tooltip)]
        [TestCase(PopupId.Reward)]
        public void CanonicalPopupFactoryPath_InstantiatesCanonicalPopupPrefabUnderPopupLayer(PopupId popupId)
        {
            var rootObject = new GameObject($"CanonicalPopupFactoryPath_{popupId}");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts());

                var popupView = OpenPopup(installer, popupId);
                var popupPrefabView = LoadPopupPrefabView(popupId);

                Assert.That(popupView, Is.Not.Null, popupId.ToString());
                Assert.That(popupPrefabView, Is.Not.Null, popupId.ToString());
                Assert.That(popupView.transform.parent, Is.EqualTo(installer.PopupLayerView.ContentRoot), popupId.ToString());
                Assert.That(popupView, Is.Not.SameAs(popupPrefabView), popupId.ToString());
                Assert.That(popupView.GetType(), Is.EqualTo(popupPrefabView.GetType()), popupId.ToString());
                Assert.That(popupView.gameObject.name, Does.StartWith(popupPrefabView.gameObject.name), popupId.ToString());
                Assert.That(popupView.gameObject.scene.IsValid(), Is.True, popupId.ToString());
                Assert.That(installer.PopupController.Contains(popupId), Is.True, popupId.ToString());
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void TooltipPopupPrefab_PreservesLocalAnchorPresentation_WithoutOwningLifecycle()
        {
            var parentObject = new GameObject("TooltipPopupPrefabParent", typeof(RectTransform));

            try
            {
                var tooltipView = UnityEngine.Object.Instantiate(
                    UiTestPrefabAssetUtility.LoadPopupPrefab<TooltipPopupView>(UiTestPrefabAssetUtility.TooltipPopupPrefabPath),
                    parentObject.transform,
                    false);
                var viewModel = new TooltipPopupViewModel();

                viewModel.SetContent("Tip", "Body", TooltipPopupAnchorPreset.UpperRight);
                tooltipView.Bind(viewModel);
                tooltipView.IsVisible = true;

                var tooltipRect = (RectTransform)tooltipView.transform;
                Assert.That(tooltipView.AnchorPreset, Is.EqualTo(TooltipPopupAnchorPreset.UpperRight));
                Assert.That(tooltipRect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(tooltipRect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));

                viewModel.SetContent("Tip", "Body", TooltipPopupAnchorPreset.LowerLeft);

                Assert.That(tooltipView.AnchorPreset, Is.EqualTo(TooltipPopupAnchorPreset.LowerLeft));
                Assert.That(tooltipRect.anchorMin, Is.EqualTo(new Vector2(0f, 0f)));
                Assert.That(tooltipRect.anchorMax, Is.EqualTo(new Vector2(0f, 0f)));
            }
            finally
            {
                if (parentObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(parentObject);
                }
            }
        }

        [Test]
        public void UiPrefabMigrationInventory_Allowlist_IsEmpty_AfterScreenPrefabMigrationCompletes()
        {
            Assert.That(ReadInventoryBoolProperty("IsRootShellMigrated"), Is.True);
            Assert.That(ReadInventoryBoolProperty("AllowsLegacyInventoryScreenSections"), Is.False);
            Assert.That(
                InvokeInventoryBooleanMethod(
                    "LayerHasNoMixedModeEntries",
                    ReadInventoryEnumValue("Game.Feature.UI.Composition.UiPrefabMigrationEntryKind", "Popup")),
                Is.True);
            Assert.That(
                InvokeInventoryBooleanMethod(
                    "LayerHasNoMixedModeEntries",
                    ReadInventoryEnumValue("Game.Feature.UI.Composition.UiPrefabMigrationEntryKind", "Screen")),
                Is.True);
            Assert.That(
                InvokeInventoryBooleanMethod(
                    "LayerHasNoMixedModeEntries",
                    ReadInventoryEnumValue("Game.Feature.UI.Composition.UiPrefabMigrationEntryKind", "ScreenInternal")),
                Is.True);

            foreach (PopupId popupId in Enum.GetValues(typeof(PopupId)))
            {
                if (popupId == PopupId.None)
                {
                    continue;
                }

                Assert.That(InvokeInventoryBooleanMethod("AllowsLegacyPopupBuilder", popupId), Is.False, popupId.ToString());
            }

            foreach (ScreenId screenId in Enum.GetValues(typeof(ScreenId)))
            {
                if (screenId == ScreenId.None)
                {
                    continue;
                }

                Assert.That(InvokeInventoryBooleanMethod("AllowsLegacyScreenBuilder", screenId), Is.False, screenId.ToString());
            }

            var documentationTokens = ReadInventoryDocumentationTokens();
            Assert.That(documentationTokens, Is.Empty);
            Assert.That(documentationTokens.Any(token => token.StartsWith("RootShell:", StringComparison.Ordinal)), Is.False);
            Assert.That(documentationTokens.Any(token => token.StartsWith("Hud:", StringComparison.Ordinal)), Is.False);
            Assert.That(documentationTokens.Any(token => token.StartsWith("Popup:", StringComparison.Ordinal)), Is.False);
            Assert.That(documentationTokens.Any(token => token.StartsWith("Screen:", StringComparison.Ordinal)), Is.False);
            Assert.That(documentationTokens.Any(token => token.StartsWith("ScreenInternal:", StringComparison.Ordinal)), Is.False);
        }

        [TestCase(ScreenId.Gameplay, "_screenPrefabCatalog.GameplayPrefab", "CreateGameplayScreen(")]
        [TestCase(ScreenId.Help, "_screenPrefabCatalog.HelpPrefab", "CreateHelpScreen(")]
        [TestCase(ScreenId.ObjectiveStatus, "_screenPrefabCatalog.ObjectiveStatusPrefab", "CreateObjectiveStatusScreen(")]
        [TestCase(ScreenId.Inventory, "_screenPrefabCatalog.InventoryPrefab", "CreateInventoryScreen(")]
        [TestCase(ScreenId.Settings, "_screenPrefabCatalog.SettingsPrefab", "CreateSettingsScreen(")]
        [TestCase(ScreenId.StageResult, "_screenPrefabCatalog.StageResultPrefab", "CreateStageResultScreen(")]
        public void ScreenFactoryMigration_UsesCanonicalPrefabReference_AndRemovesLegacyBuilderMarkers(
            ScreenId screenId,
            string prefabReferenceToken,
            string legacyBuilderToken)
        {
            var screenFactorySource = ReadRepoFile(ScreenFactorySourcePath);

            Assert.That(screenFactorySource, Does.Contain(prefabReferenceToken), screenId.ToString());
            Assert.That(screenFactorySource, Does.Not.Contain(legacyBuilderToken), screenId.ToString());
            Assert.That(screenFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreatePanel("), screenId.ToString());
            Assert.That(screenFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreateLabel("), screenId.ToString());
            Assert.That(screenFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreateButton("), screenId.ToString());
        }

        [Test]
        public void ScreenLegacyBuilderSymbols_AreAbsent_FromFactorySource_Inventory_AndBaselineEvidence()
        {
            var installerSource = ReadRepoFile(InstallerSourcePath);
            var screenFactorySource = ReadRepoFile(ScreenFactorySourcePath);
            var baseline = ReadRepoFile(BaselineNotePath);

            Assert.That(installerSource, Does.Contain("_screenPrefabCatalog"));
            Assert.That(installerSource, Does.Not.Contain("ScreenPrefabRegistry"));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateGameplayScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateHelpScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateObjectiveStatusScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateInventoryScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateSettingsScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateStageResultScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreatePanel("));
            Assert.That(screenFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreateLabel("));
            Assert.That(screenFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreateButton("));
            Assert.That(baseline, Does.Contain("screen legacy runtime builder paths were removed in the same phase"));
            Assert.That(baseline, Does.Contain("screen hybrid allowlist is now empty"));
            Assert.That(baseline, Does.Not.Contain("Screen:Gameplay -> GameplayScreenRuntimeFactory.CreateGameplayScreen"));
            Assert.That(baseline, Does.Not.Contain("ScreenInternal:InventoryScreen.Sections -> InventoryScreenView authored child sections remain runtime-built"));
        }

        [TestCase(PopupId.Pause, "_popupPrefabCatalog.PausePrefab", "AddComponent<PausePopupView>")]
        [TestCase(PopupId.ObjectiveInfo, "_popupPrefabCatalog.ObjectiveInfoPrefab", "AddComponent<ObjectiveInfoPopupView>")]
        [TestCase(PopupId.Confirm, "_popupPrefabCatalog.ConfirmPrefab", "AddComponent<ConfirmPopupView>")]
        [TestCase(PopupId.Tooltip, "_popupPrefabCatalog.TooltipPrefab", "AddComponent<TooltipPopupView>")]
        [TestCase(PopupId.Reward, "_popupPrefabCatalog.RewardPrefab", "AddComponent<RewardPopupView>")]
        public void PopupFactoryMigration_UsesCanonicalPrefabReference_AndRemovesLegacyBuilderMarker(
            PopupId popupId,
            string prefabReferenceToken,
            string legacyBuilderToken)
        {
            var popupFactorySource = ReadRepoFile(PopupFactorySourcePath);

            Assert.That(popupFactorySource, Does.Contain(prefabReferenceToken), popupId.ToString());
            Assert.That(popupFactorySource, Does.Not.Contain(legacyBuilderToken), popupId.ToString());
            Assert.That(popupFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreatePanel("), popupId.ToString());
        }

        [Test]
        public void PopupLegacyBuilderSymbols_AreAbsent_FromFactorySource_Inventory_AndBaselineEvidence()
        {
            var installerSource = ReadRepoFile(InstallerSourcePath);
            var popupFactorySource = ReadRepoFile(PopupFactorySourcePath);
            var baseline = ReadRepoFile(BaselineNotePath);

            Assert.That(installerSource, Does.Contain("_popupPrefabCatalog"));
            Assert.That(installerSource, Does.Not.Contain("GameplayPopupPrefabRegistry"));
            Assert.That(popupFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreatePanel("));
            Assert.That(popupFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreateLabel("));
            Assert.That(popupFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreateButton("));
            Assert.That(baseline, Does.Contain("popup legacy runtime builder paths were removed in the same phase"));
            Assert.That(baseline, Does.Not.Contain("Popup:Pause -> GameplayPopupRuntimeFactory.CreatePausePopup"));
        }

        [Test]
        public void PopupViewSource_RemainsLocalOnly_AndDoesNotIntroduceTimerOrLifecycleOwnershipApis()
        {
            var popupViewSources = new[]
            {
                ReadRepoFile(PausePopupViewSourcePath),
                ReadRepoFile(ObjectiveInfoPopupViewSourcePath),
                ReadRepoFile(ConfirmPopupViewSourcePath),
                ReadRepoFile(TooltipPopupViewSourcePath),
                ReadRepoFile(RewardPopupViewSourcePath),
            };

            foreach (var popupViewSource in popupViewSources)
            {
                Assert.That(popupViewSource, Does.Not.Contain("Object.Destroy("));
                Assert.That(popupViewSource, Does.Not.Contain("DestroyImmediate("));
                Assert.That(popupViewSource, Does.Not.Contain(".Dispose("));
                Assert.That(popupViewSource, Does.Not.Contain("CloseTop"));
                Assert.That(popupViewSource, Does.Not.Contain("CloseAll"));
                Assert.That(popupViewSource, Does.Not.Contain("PopupController"));
                Assert.That(popupViewSource, Does.Not.Contain("UIFlowCoordinator"));
                Assert.That(popupViewSource, Does.Not.Contain("ScreenController"));
                Assert.That(popupViewSource, Does.Not.Contain("IGameplayQueryFacade"));
                Assert.That(popupViewSource, Does.Not.Contain("IGameplayCommandGateway"));
                Assert.That(popupViewSource, Does.Not.Contain("StartCoroutine("));
                Assert.That(popupViewSource, Does.Not.Contain("InvokeRepeating("));
                Assert.That(popupViewSource, Does.Not.Contain("CancelInvoke("));
                Assert.That(popupViewSource, Does.Not.Contain("Invoke(nameof("));
                Assert.That(popupViewSource, Does.Not.Contain("Invoke(\""));
                Assert.That(popupViewSource, Does.Not.Contain("WaitForSeconds"));
            }

            var tooltipPopupViewSource = ReadRepoFile(TooltipPopupViewSourcePath);
            Assert.That(tooltipPopupViewSource.ToLowerInvariant(), Does.Not.Contain("autohide"));
            Assert.That(tooltipPopupViewSource.ToLowerInvariant(), Does.Not.Contain("expiry"));
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
            return ((IEnumerable)property.GetValue(null))
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

        private static object ReadInventoryEnumValue(string enumTypeName, string valueName)
        {
            var enumType = typeof(GameplayUiFlowInstaller).Assembly.GetType(enumTypeName);
            Assert.That(enumType, Is.Not.Null, enumTypeName);
            return Enum.Parse(enumType, valueName);
        }

        private static Type GetInventoryType()
        {
            var inventoryType = typeof(GameplayUiFlowInstaller).Assembly.GetType("Game.Feature.UI.Composition.UiPrefabMigrationInventory");
            Assert.That(inventoryType, Is.Not.Null);
            return inventoryType;
        }

        private static void AssertScreenPrefabContract<TScreenView>(string assetPath, params Type[] allowedViewTypes)
            where TScreenView : Component
        {
            var screenPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<TScreenView>(assetPath);

            Assert.That(screenPrefab, Is.Not.Null, assetPath);
            AssertScreenPrefabHasNoCrossLayerOwners(screenPrefab.gameObject, allowedViewTypes);
        }

        private static void AssertPopupPrefabContract<TPopupView>(string assetPath)
            where TPopupView : Component
        {
            var popupPrefab = UiTestPrefabAssetUtility.LoadPopupPrefab<TPopupView>(assetPath);

            Assert.That(popupPrefab, Is.Not.Null, assetPath);
            Assert.That(popupPrefab.GetComponent<CanvasGroup>(), Is.Not.Null, assetPath);
            AssertPrefabHasNoCrossLayerOwners(popupPrefab.gameObject);
        }

        private static Component OpenMountedScreen(GameplayUiFlowInstaller installer, ScreenId screenId)
        {
            switch (screenId)
            {
                case ScreenId.Gameplay:
                    return installer.GameplayScreenView;

                case ScreenId.Help:
                    installer.GameplayScreenView.ClickHelp();
                    return installer.HelpScreenView;

                case ScreenId.ObjectiveStatus:
                    installer.GameplayScreenView.ClickObjectives();
                    return installer.ObjectiveStatusScreenView;

                case ScreenId.Inventory:
                    installer.GameplayScreenView.ClickInventory();
                    return installer.InventoryScreenView;

                case ScreenId.Settings:
                    installer.GameplayScreenView.ClickSettings();
                    return installer.SettingsScreenView;

                default:
                    throw new ArgumentOutOfRangeException(nameof(screenId), screenId, null);
            }
        }

        private static Component OpenPopup(GameplayUiFlowInstaller installer, PopupId popupId)
        {
            switch (popupId)
            {
                case PopupId.Pause:
                    installer.HudView.ClickPause();
                    return installer.PausePopupView;

                case PopupId.ObjectiveInfo:
                    installer.GameplayScreenView.ClickObjectives();
                    installer.ObjectiveStatusScreenView.ClickInfo();
                    return installer.ObjectiveInfoPopupView;

                case PopupId.Confirm:
                    installer.Coordinator.RequestConfirmPopup(
                        new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false));
                    return installer.ConfirmPopupView;

                case PopupId.Tooltip:
                    installer.GameplayScreenView.ClickSettings();
                    installer.SettingsScreenView.ClickTooltipInfo();
                    return installer.TooltipPopupView;

                case PopupId.Reward:
                    installer.Coordinator.RequestRewardPopup(
                        new RewardPopupPayload(
                            "Reward",
                            new[] { new RewardPopupItemPayload("Crystal", 2) },
                            "Summary",
                            "Claim"));
                    return installer.RewardPopupView;

                default:
                    throw new ArgumentOutOfRangeException(nameof(popupId), popupId, null);
            }
        }

        private static Component LoadScreenPrefabView(ScreenId screenId)
        {
            switch (screenId)
            {
                case ScreenId.Gameplay:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<GameplayScreenView>(UiTestPrefabAssetUtility.GameplayScreenPrefabPath);

                case ScreenId.Help:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<HelpScreenView>(UiTestPrefabAssetUtility.HelpScreenPrefabPath);

                case ScreenId.ObjectiveStatus:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<ObjectiveStatusScreenView>(UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath);

                case ScreenId.Inventory:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<InventoryScreenView>(UiTestPrefabAssetUtility.InventoryScreenPrefabPath);

                case ScreenId.Settings:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);

                case ScreenId.StageResult:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<StageResultScreenView>(UiTestPrefabAssetUtility.StageResultScreenPrefabPath);

                default:
                    throw new ArgumentOutOfRangeException(nameof(screenId), screenId, null);
            }
        }

        private static string GetPrefabAssetPath(PopupId popupId)
        {
            switch (popupId)
            {
                case PopupId.Pause:
                    return UiTestPrefabAssetUtility.PausePopupPrefabPath;

                case PopupId.ObjectiveInfo:
                    return UiTestPrefabAssetUtility.ObjectiveInfoPopupPrefabPath;

                case PopupId.Confirm:
                    return UiTestPrefabAssetUtility.ConfirmPopupPrefabPath;

                case PopupId.Tooltip:
                    return UiTestPrefabAssetUtility.TooltipPopupPrefabPath;

                case PopupId.Reward:
                    return UiTestPrefabAssetUtility.RewardPopupPrefabPath;

                default:
                    throw new ArgumentOutOfRangeException(nameof(popupId), popupId, null);
            }
        }

        private static Component LoadPopupPrefabView(PopupId popupId)
        {
            switch (popupId)
            {
                case PopupId.Pause:
                    return UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(UiTestPrefabAssetUtility.PausePopupPrefabPath);

                case PopupId.ObjectiveInfo:
                    return UiTestPrefabAssetUtility.LoadPopupPrefab<ObjectiveInfoPopupView>(UiTestPrefabAssetUtility.ObjectiveInfoPopupPrefabPath);

                case PopupId.Confirm:
                    return UiTestPrefabAssetUtility.LoadPopupPrefab<ConfirmPopupView>(UiTestPrefabAssetUtility.ConfirmPopupPrefabPath);

                case PopupId.Tooltip:
                    return UiTestPrefabAssetUtility.LoadPopupPrefab<TooltipPopupView>(UiTestPrefabAssetUtility.TooltipPopupPrefabPath);

                case PopupId.Reward:
                    return UiTestPrefabAssetUtility.LoadPopupPrefab<RewardPopupView>(UiTestPrefabAssetUtility.RewardPopupPrefabPath);

                default:
                    throw new ArgumentOutOfRangeException(nameof(popupId), popupId, null);
            }
        }

        private static void AssertPrefabHasNoCrossLayerOwners(GameObject prefabRoot)
        {
            var forbiddenTypes = new[]
            {
                typeof(GameplayUiCanvasRootView),
                typeof(HUDRootView),
                typeof(PlayerStatusView),
                typeof(ActionBarView),
                typeof(NotificationView),
                typeof(PopupLayerView),
                typeof(GameplayScreenView),
                typeof(HelpScreenView),
                typeof(ObjectiveStatusScreenView),
                typeof(InventoryScreenView),
                typeof(SettingsScreenView),
                typeof(SettingsAudioView),
                typeof(SettingsDisplayView),
                typeof(StageResultScreenView),
                typeof(ScreenLayerView),
                typeof(UiArchitectureDiagnosticsOverlayView),
                typeof(PopupController),
                typeof(UIFlowCoordinator),
                typeof(ScreenController),
            };

            var foundForbiddenComponents = prefabRoot
                .GetComponentsInChildren<Component>(true)
                .Where(component => component != null && forbiddenTypes.Contains(component.GetType()))
                .Select(component => component.GetType().Name)
                .Distinct()
                .ToArray();

            Assert.That(foundForbiddenComponents, Is.Empty);
        }

        private static void AssertScreenPrefabHasNoCrossLayerOwners(GameObject prefabRoot, params Type[] allowedViewTypes)
        {
            var screenViewTypes = new[]
            {
                typeof(GameplayScreenView),
                typeof(HelpScreenView),
                typeof(ObjectiveStatusScreenView),
                typeof(InventoryScreenView),
                typeof(SettingsScreenView),
                typeof(SettingsAudioView),
                typeof(SettingsDisplayView),
                typeof(StageResultScreenView),
                typeof(InventoryCatalogView),
                typeof(InventoryDetailView),
                typeof(InventoryActionView),
            };

            var forbiddenTypes = new[]
            {
                typeof(GameplayUiCanvasRootView),
                typeof(HUDRootView),
                typeof(PlayerStatusView),
                typeof(ActionBarView),
                typeof(NotificationView),
                typeof(PopupLayerView),
                typeof(PausePopupView),
                typeof(ObjectiveInfoPopupView),
                typeof(ConfirmPopupView),
                typeof(TooltipPopupView),
                typeof(RewardPopupView),
                typeof(ScreenLayerView),
                typeof(UiArchitectureDiagnosticsOverlayView),
                typeof(PopupController),
                typeof(UIFlowCoordinator),
                typeof(ScreenController),
            }
            .Concat(screenViewTypes.Except(allowedViewTypes ?? Array.Empty<Type>()))
            .ToArray();

            var foundForbiddenComponents = prefabRoot
                .GetComponentsInChildren<Component>(true)
                .Where(component => component != null && forbiddenTypes.Contains(component.GetType()))
                .Select(component => component.GetType().Name)
                .Distinct()
                .ToArray();

            Assert.That(foundForbiddenComponents, Is.Empty);
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }

        private static void AssertSerializedComponentPropertyAssignedAndUnderRoot(SerializedObject serializedObject, string propertyPath, Transform expectedRoot)
        {
            var property = serializedObject.FindProperty(propertyPath);
            Assert.That(property, Is.Not.Null, propertyPath);
            Assert.That(property.objectReferenceValue, Is.Not.Null, propertyPath);
            switch (property.objectReferenceValue)
            {
                case Component component:
                    Assert.That(component.transform.IsChildOf(expectedRoot), Is.True, propertyPath);
                    break;

                case GameObject gameObject:
                    Assert.That(gameObject.transform.IsChildOf(expectedRoot), Is.True, propertyPath);
                    break;

                default:
                    Assert.Fail($"{propertyPath} must reference a Component or GameObject.");
                    break;
            }
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
