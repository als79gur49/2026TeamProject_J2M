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
using TMPro;
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
                typeof(ObjectiveStatusScreenView),
                typeof(SettingsScreenView),
                typeof(SettingsAudioView),
                typeof(SettingsDisplayView),
                typeof(StageResultScreenView),
                typeof(LevelFailedScreenView),
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

                UiTestPrefabAssetUtility.AssertOverlayCanvasScaling(rootShellInstance);
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
            Assert.That(rootShellPrefab.GetComponentsInChildren<Text>(true), Is.Empty);
            Assert.That(hudLayer.GetComponentsInChildren<Button>(true), Is.Empty);
            Assert.That(hudLayer.GetComponentsInChildren<TMP_Text>(true), Is.Empty);
            Assert.That(hudLayer.GetComponentsInChildren<CanvasGroup>(true), Is.Empty);
        }

        [Test]
        public void CanonicalRootShellPrefab_AuthorsScreenPopupAndDiagnosticsInfrastructure()
        {
            var rootShellPrefab = Resources.Load<GameObject>(RootShellResourcePath);
            Assert.That(rootShellPrefab, Is.Not.Null);

            var screenLayerRoot = FindRequired(rootShellPrefab.transform, "ScreenLayer/ScreenLayerRoot");
            var screenContentRoot = FindRequired(screenLayerRoot, "ScreenContentRoot");
            var screenLayerView = RequireComponent<ScreenLayerView>(screenLayerRoot);
            Assert.That(screenLayerView.ContentRoot, Is.EqualTo(screenContentRoot));

            var popupLayerRoot = FindRequired(rootShellPrefab.transform, "PopupLayer/PopupLayerRoot");
            var backdrop = FindRequired(popupLayerRoot, "Backdrop");
            var popupContentRoot = FindRequired(popupLayerRoot, "PopupContentRoot");
            var popupLayerView = RequireComponent<PopupLayerView>(popupLayerRoot);
            Assert.That(popupLayerView.ContentRoot, Is.EqualTo(popupContentRoot));
            RequireComponent<Image>(backdrop);
            RequireComponent<Button>(backdrop);
            RequireComponent<CanvasGroup>(backdrop);

            var diagnosticsOverlay = FindRequired(rootShellPrefab.transform, "DiagnosticsLayer/UiDiagnosticsOverlay");
            RequireComponent<UiArchitectureDiagnosticsOverlayView>(diagnosticsOverlay);
            RequireComponent<CanvasGroup>(diagnosticsOverlay);
            RequireComponent<TMP_Text>(FindRequired(diagnosticsOverlay, "Title"));
            RequireComponent<TMP_Text>(FindRequired(diagnosticsOverlay, "Summary"));
            RequireComponent<Image>(FindRequired(diagnosticsOverlay, "DetailPanel"));
            RequireComponent<TMP_Text>(FindRequired(diagnosticsOverlay, "DetailPanel/Detail"));
        }

        [Test]
        public void CanonicalHudPrefabAsset_UsesAuthoredChildViews_AndNoCrossLayerOwners()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();

            Assert.That(hudPrefab, Is.Not.Null);
            Assert.That(hudPrefab.PlayerStatusView, Is.Not.Null);
            Assert.That(hudPrefab.NotificationView, Is.Not.Null);
            Assert.That(hudPrefab.GetComponentsInChildren<ActionBarView>(true).Single().gameObject.activeSelf, Is.False);
            Assert.That(hudPrefab.GetComponentsInChildren<GameplayUiCanvasRootView>(true), Is.Empty);
            Assert.That(hudPrefab.GetComponentsInChildren<ScreenLayerView>(true), Is.Empty);
            Assert.That(hudPrefab.GetComponentsInChildren<PopupLayerView>(true), Is.Empty);
            Assert.That(hudPrefab.GetComponentsInChildren<PausePopupView>(true), Is.Empty);
        }

        [Test]
        public void HudPrefabAsset_AuthorsPersistentStackLayout_BeforeRuntimeLayoutRemoval()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();

            var topLeftStack = FindRequired(hudPrefab.transform, "HudTopLeftStack");
            var topRightStack = FindRequired(hudPrefab.transform, "HudTopRightStack");
            var bottomRightStack = FindRequired(hudPrefab.transform, "HudBottomRightStack");

            RequireComponent<VerticalLayoutGroup>(topLeftStack);
            RequireComponent<VerticalLayoutGroup>(topRightStack);
            RequireComponent<VerticalLayoutGroup>(bottomRightStack);
            Assert.That(hudPrefab.ObjectiveHudView.transform.parent, Is.EqualTo(topLeftStack));
            Assert.That(hudPrefab.NotificationView.transform.parent, Is.EqualTo(bottomRightStack));
            var chancePanelView = hudPrefab.GetComponentsInChildren<ChancePanelView>(true).Single();
            var topologyBeltView = hudPrefab.GetComponentsInChildren<TopologyBeltView>(true).Single();
            Assert.That(chancePanelView.transform.parent, Is.EqualTo(bottomRightStack));
            Assert.That(topologyBeltView.transform.parent, Is.EqualTo(topRightStack));
            Assert.That(hudPrefab.ObjectiveHudView.GetComponent<LayoutElement>(), Is.Not.Null);
            Assert.That(hudPrefab.NotificationView.GetComponent<LayoutElement>(), Is.Not.Null);
            Assert.That(chancePanelView.GetComponent<LayoutElement>(), Is.Not.Null);
            Assert.That(topologyBeltView.GetComponent<LayoutElement>(), Is.Not.Null);
        }

        [Test]
        public void ProjectOwnedUiPrefabs_NoLongerSerializeLegacyTextComponents()
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Features/UI" });
            var prefabsWithLegacyText = prefabGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (Path: path, Prefab: AssetDatabase.LoadAssetAtPath<GameObject>(path)))
                .Where(entry => entry.Prefab != null && entry.Prefab.GetComponentsInChildren<Text>(true).Length > 0)
                .Select(entry => entry.Path)
                .OrderBy(path => path)
                .ToArray();

            Assert.That(prefabsWithLegacyText, Is.Empty);
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
                    "_objectiveStatusPrefab",
                    "_settingsPrefab",
                    "_stageResultPrefab",
                    "_levelFailedPrefab",
                }));
            Assert.That(
                instanceFields.Select(field => field.FieldType).ToArray(),
                Is.EqualTo(new[]
                {
                    typeof(ObjectiveStatusScreenView),
                    typeof(SettingsScreenView),
                    typeof(StageResultScreenView),
                    typeof(LevelFailedScreenView),
                }));
            Assert.That(
                publicPropertyNames,
                Is.EqualTo(new[]
                {
                    "LevelFailedPrefab",
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

            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.ObjectiveStatusPrefab), Is.EqualTo(UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.SettingsPrefab), Is.EqualTo(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.StageResultPrefab), Is.EqualTo(UiTestPrefabAssetUtility.StageResultScreenPrefabPath));
            Assert.That(AssetDatabase.GetAssetPath(screenCatalog.LevelFailedPrefab), Is.EqualTo(UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath));
        }

        [Test]
        public void PausePopupPrefabAsset_UsesAuthoredPopupView_AndNoCrossLayerOwners()
        {
            AssertPopupPrefabContract<PausePopupView>(UiTestPrefabAssetUtility.PausePopupPrefabPath);
        }

        [Test]
        public void PausePopupPrefabAsset_WiresObjectiveAndSettingsButtonsAndLabels_BeneathPopupRoot()
        {
            var pausePopup = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(UiTestPrefabAssetUtility.PausePopupPrefabPath);
            var serializedPausePopup = new SerializedObject(pausePopup);

            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedPausePopup, "_objectiveButton", pausePopup.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedPausePopup, "_objectiveButtonLabel", pausePopup.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedPausePopup, "_settingsButton", pausePopup.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedPausePopup, "_settingsButtonLabel", pausePopup.transform);
        }

        [Test]
        public void ObjectiveStatusScreenPrefabAsset_RemovesLegacyOverviewAndSessionTabButtons()
        {
            var screen = UiTestPrefabAssetUtility.LoadScreenPrefab<ObjectiveStatusScreenView>(UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath);
            var serializedScreen = new SerializedObject(screen);
            var childNames = screen.GetComponentsInChildren<Transform>(true)
                .Select(child => child.name)
                .ToArray();

            Assert.That(serializedScreen.FindProperty("_overviewButton"), Is.Null);
            Assert.That(serializedScreen.FindProperty("_sessionButton"), Is.Null);
            Assert.That(childNames, Does.Not.Contain("OverviewButton"));
            Assert.That(childNames, Does.Not.Contain("SessionButton"));
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

        [TestCase(ScreenId.ObjectiveStatus)]
        [TestCase(ScreenId.Settings)]
        [TestCase(ScreenId.StageResult)]
        [TestCase(ScreenId.LevelFailed)]
        public void CanonicalScreenPrefabAsset_UsesAuthoredScreenView_AndNoCrossLayerOwners(ScreenId screenId)
        {
            switch (screenId)
            {
                case ScreenId.ObjectiveStatus:
                    AssertScreenPrefabContract<ObjectiveStatusScreenView>(
                        UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath,
                        typeof(ObjectiveStatusScreenView));
                    break;

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

                case ScreenId.LevelFailed:
                    AssertScreenPrefabContract<LevelFailedScreenView>(
                        UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath,
                        typeof(LevelFailedScreenView));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(screenId), screenId, null);
            }
        }

        [Test]
        public void ScreenPrefabs_AuthorStaticLayoutContainers_BeforeRuntimeLayoutRemoval()
        {
            var mainMenuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab");
            Assert.That(mainMenuPrefab, Is.Not.Null);
            var mainMenuRoot = mainMenuPrefab.transform;
            RequireComponent<VerticalLayoutGroup>(mainMenuRoot);
            RequireComponent<LayoutElement>(FindRequired(mainMenuRoot, "TopBar"));
            RequireComponent<LayoutElement>(FindRequired(mainMenuRoot, "ContentHost"));
            RequireComponent<LayoutElement>(FindRequired(mainMenuRoot, "BottomBar"));
            RequireComponent<VerticalLayoutGroup>(FindRequired(mainMenuRoot, "MainCommandPanel"));
            RequireComponent<LayoutElement>(FindRequired(mainMenuRoot, "ContentHost/SaveSlotPanelView"));

            var settingsRoot = UiTestPrefabAssetUtility
                .LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath)
                .transform;
            var settingsRootLayout = RequireComponent<VerticalLayoutGroup>(settingsRoot);
            Assert.That(settingsRoot.GetChild(0).name, Is.EqualTo("Background"));
            Assert.That(settingsRootLayout.padding.top, Is.EqualTo(20));
            Assert.That(settingsRootLayout.padding.bottom, Is.EqualTo(20));
            RequireComponent<HorizontalLayoutGroup>(FindRequired(settingsRoot, "SettingsHeader"));
            RequireComponent<HorizontalLayoutGroup>(FindRequired(settingsRoot, "SettingsTabRow"));
            var sectionHost = FindRequired(settingsRoot, "SettingsSectionHost");
            RequireComponent<VerticalLayoutGroup>(sectionHost);
            var sectionHostLayout = RequireComponent<LayoutElement>(sectionHost);
            Assert.That(sectionHostLayout.minHeight, Is.EqualTo(320f));
            Assert.That(sectionHostLayout.preferredHeight, Is.EqualTo(432f));
            AssertSettingsSectionLayout(FindRequired(sectionHost, SettingsScreenView.AudioSectionName));
            AssertSettingsSectionLayout(FindRequired(sectionHost, SettingsScreenView.DisplaySectionName));
            AssertSettingsSectionLayout(FindRequired(sectionHost, SettingsScreenView.InputSectionName));
            var accessibilityRows = FindRequired(settingsRoot, "SettingsAccessibilityRows");
            Assert.That(accessibilityRows.gameObject.activeSelf, Is.False);
            Assert.That(RequireComponent<LayoutElement>(accessibilityRows).ignoreLayout, Is.True);
            RequireComponent<HorizontalLayoutGroup>(FindRequired(settingsRoot, "SettingsFooter"));
            RequireComponent<HorizontalLayoutGroup>(FindRequired(sectionHost, "SettingsDisplaySection/CurrentDisplayRow"));
            RequireComponent<HorizontalLayoutGroup>(FindRequired(sectionHost, "SettingsDisplaySection/ResolutionRow"));
            RequireComponent<HorizontalLayoutGroup>(FindRequired(sectionHost, "SettingsDisplaySection/FullscreenRow"));
            RequireComponent<HorizontalLayoutGroup>(FindRequired(sectionHost, "SettingsDisplaySection/DisplayActionRow"));
            RequireComponent<CanvasGroup>(FindRequired(sectionHost, "SettingsDisplaySection/ResolutionHoverHint"));
            RequireComponent<LayoutElement>(FindRequired(sectionHost, "SettingsDisplaySection/DisplayPreviewCountdown"));
            RequireComponent<HorizontalLayoutGroup>(FindRequired(sectionHost, "SettingsInputSection/MovementInputRow"));
            var pushInputRow = FindRequired(sectionHost, "SettingsInputSection/PushInputRow");
            var flipInputRow = FindRequired(sectionHost, "SettingsInputSection/FlipInputRow");
            var inputResetRow = FindRequired(sectionHost, "SettingsInputSection/InputResetRow");
            RequireComponent<HorizontalLayoutGroup>(pushInputRow);
            RequireComponent<HorizontalLayoutGroup>(flipInputRow);
            RequireComponent<HorizontalLayoutGroup>(inputResetRow);
            Assert.That(RequireComponent<LayoutElement>(pushInputRow).preferredHeight, Is.EqualTo(40f));
            Assert.That(RequireComponent<LayoutElement>(flipInputRow).preferredHeight, Is.EqualTo(40f));
            Assert.That(RequireComponent<LayoutElement>(inputResetRow).preferredHeight, Is.EqualTo(40f));

            AssertObjectiveAssetLayout();
            AssertTerminalResultLayout(
                UiTestPrefabAssetUtility.LoadScreenPrefab<StageResultScreenView>(UiTestPrefabAssetUtility.StageResultScreenPrefabPath).transform,
                "ResultSummary");
            AssertTerminalResultLayout(
                UiTestPrefabAssetUtility.LoadScreenPrefab<LevelFailedScreenView>(UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath).transform);
        }

        [Test]
        public void SettingsScreenPrefabAsset_AuthorsRequiredChildSections_AndSerializedChildRefs()
        {
            var settingsPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var serializedRoot = new SerializedObject(settingsPrefab);
            var audioViewProperty = serializedRoot.FindProperty("_audioView");
            var displayViewProperty = serializedRoot.FindProperty("_displayView");
            var inputViewProperty = serializedRoot.FindProperty("_inputView");

            Assert.That(audioViewProperty, Is.Not.Null);
            Assert.That(displayViewProperty, Is.Not.Null);
            Assert.That(inputViewProperty, Is.Not.Null);
            Assert.That(audioViewProperty.objectReferenceValue, Is.Not.Null);
            Assert.That(displayViewProperty.objectReferenceValue, Is.Not.Null);
            Assert.That(inputViewProperty.objectReferenceValue, Is.Not.Null);
            Assert.That(serializedRoot.FindProperty("_mainRow"), Is.Null);
            Assert.That(serializedRoot.FindProperty("_resolutionDropdown"), Is.Null);
            Assert.That(serializedRoot.FindProperty("_applyButton"), Is.Null);

            var audioView = (SettingsAudioView)audioViewProperty.objectReferenceValue;
            var displayView = (SettingsDisplayView)displayViewProperty.objectReferenceValue;
            var inputView = (SettingsInputView)inputViewProperty.objectReferenceValue;
            var sectionHost = settingsPrefab.transform.Find("SettingsSectionHost");

            Assert.That(audioView.name, Is.EqualTo(SettingsScreenView.AudioSectionName));
            Assert.That(displayView.name, Is.EqualTo(SettingsScreenView.DisplaySectionName));
            Assert.That(inputView.name, Is.EqualTo(SettingsScreenView.InputSectionName));
            Assert.That(audioView.transform.parent, Is.EqualTo(sectionHost));
            Assert.That(displayView.transform.parent, Is.EqualTo(sectionHost));
            Assert.That(inputView.transform.parent, Is.EqualTo(sectionHost));

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
            Assert.That(((TMP_Text)serializedAudio.FindProperty("_mainRow._label").objectReferenceValue).text, Is.EqualTo("Main"));
            Assert.That(((TMP_Text)serializedAudio.FindProperty("_bgmRow._label").objectReferenceValue).text, Is.EqualTo("Background Music"));
            Assert.That(((TMP_Text)serializedAudio.FindProperty("_sfxRow._label").objectReferenceValue).text, Is.EqualTo("Effects"));

            var serializedDisplay = new SerializedObject(displayView);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_sectionTitle", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_currentDisplayLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_currentDisplayValue", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_resolutionLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_resolutionDropdown", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_resolutionInfoHotspot", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_resolutionHoverRelay", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_resolutionHoverHintRoot", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_resolutionHoverHintLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_fullscreenLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_fullscreenToggle", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_displayStatusLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_previewCountdownRoot", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_previewCountdownLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_previewCountdownFill", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_applyButton", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_applyButtonLabel", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_revertButton", displayView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedDisplay, "_revertButtonLabel", displayView.transform);

            var hoverHintRoot = (RectTransform)serializedDisplay.FindProperty("_resolutionHoverHintRoot").objectReferenceValue;
            var hoverHintLabel = (TMP_Text)serializedDisplay.FindProperty("_resolutionHoverHintLabel").objectReferenceValue;
            var hoverRelay = (SettingsHoverRelay)serializedDisplay.FindProperty("_resolutionHoverRelay").objectReferenceValue;
            var sectionTitle = (TMP_Text)serializedDisplay.FindProperty("_sectionTitle").objectReferenceValue;
            var currentDisplayLabel = (TMP_Text)serializedDisplay.FindProperty("_currentDisplayLabel").objectReferenceValue;
            var resolutionLabel = (TMP_Text)serializedDisplay.FindProperty("_resolutionLabel").objectReferenceValue;
            var fullscreenLabel = (TMP_Text)serializedDisplay.FindProperty("_fullscreenLabel").objectReferenceValue;
            var applyButtonLabel = (TMP_Text)serializedDisplay.FindProperty("_applyButtonLabel").objectReferenceValue;
            var revertButtonLabel = (TMP_Text)serializedDisplay.FindProperty("_revertButtonLabel").objectReferenceValue;
            var hoverCanvasGroup = hoverHintRoot.GetComponent<CanvasGroup>();
            var hoverImage = hoverHintRoot.GetComponent<Image>();
            var countdownRoot = (RectTransform)serializedDisplay.FindProperty("_previewCountdownRoot").objectReferenceValue;
            var countdownLabel = (TMP_Text)serializedDisplay.FindProperty("_previewCountdownLabel").objectReferenceValue;
            var countdownFill = (Image)serializedDisplay.FindProperty("_previewCountdownFill").objectReferenceValue;

            Assert.That(hoverHintRoot.gameObject.activeSelf, Is.False);
            Assert.That(sectionTitle.text, Is.EqualTo("Display"));
            Assert.That(currentDisplayLabel.text, Is.EqualTo("Current Display"));
            Assert.That(resolutionLabel.text, Is.EqualTo("Resolution"));
            Assert.That(hoverHintLabel.text, Is.EqualTo("Only automatically detected resolutions are shown."));
            Assert.That(fullscreenLabel.text, Is.EqualTo("Fullscreen Window"));
            Assert.That(applyButtonLabel.text, Is.EqualTo("Apply"));
            Assert.That(revertButtonLabel.text, Is.EqualTo("Revert"));
            Assert.That(hoverRelay.transform, Is.EqualTo(serializedDisplay.FindProperty("_resolutionInfoHotspot").objectReferenceValue));
            Assert.That(hoverCanvasGroup, Is.Not.Null);
            Assert.That(hoverCanvasGroup.blocksRaycasts, Is.False);
            Assert.That(hoverCanvasGroup.interactable, Is.False);
            Assert.That(hoverImage, Is.Not.Null);
            Assert.That(hoverImage.raycastTarget, Is.False);
            Assert.That(hoverHintLabel.raycastTarget, Is.False);
            Assert.That(countdownRoot.gameObject.activeSelf, Is.False);
            Assert.That(countdownLabel.raycastTarget, Is.False);
            Assert.That(countdownFill.raycastTarget, Is.False);
            Assert.That(countdownFill.type, Is.EqualTo(Image.Type.Simple));

            var serializedInput = new SerializedObject(inputView);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedInput, "_pushKeyDisplayLabel", inputView.transform);
            AssertSerializedComponentPropertyAssignedAndUnderRoot(serializedInput, "_flipKeyDisplayLabel", inputView.transform);

            var pushChangeButton = (Button)serializedInput.FindProperty("_pushChangeButton").objectReferenceValue;
            var pushChangeLabel = (TMP_Text)serializedInput.FindProperty("_pushChangeButtonLabel").objectReferenceValue;
            var flipChangeButton = (Button)serializedInput.FindProperty("_flipChangeButton").objectReferenceValue;
            var flipChangeLabel = (TMP_Text)serializedInput.FindProperty("_flipChangeButtonLabel").objectReferenceValue;
            var resetButton = (Button)serializedInput.FindProperty("_resetButton").objectReferenceValue;
            var resetButtonLabel = (TMP_Text)serializedInput.FindProperty("_resetButtonLabel").objectReferenceValue;
            Assert.That(pushChangeButton.name, Is.EqualTo("PushChange"));
            Assert.That(pushChangeButton.transition, Is.EqualTo(Selectable.Transition.Animation));
            Assert.That(pushChangeButton.animationTriggers.disabledTrigger, Is.EqualTo("Disabled"));
            Assert.That(pushChangeButton.animationTriggers.disabledTrigger, Is.Not.EqualTo(pushChangeButton.animationTriggers.pressedTrigger));
            Assert.That(pushChangeButton.GetComponent<Animator>(), Is.Not.Null);
            Assert.That(pushChangeLabel.name, Is.EqualTo("PushChangeLabel"));
            Assert.That(pushChangeButton.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(116f));
            Assert.That(pushChangeButton.GetComponent<LayoutElement>().preferredHeight, Is.EqualTo(34f));
            Assert.That(flipChangeButton.name, Is.EqualTo("FlipChange_New"));
            Assert.That(flipChangeButton.transition, Is.EqualTo(Selectable.Transition.Animation));
            Assert.That(flipChangeButton.animationTriggers.disabledTrigger, Is.EqualTo("Disabled"));
            Assert.That(flipChangeButton.animationTriggers.disabledTrigger, Is.Not.EqualTo(flipChangeButton.animationTriggers.pressedTrigger));
            Assert.That(flipChangeButton.GetComponent<Animator>(), Is.Not.Null);
            Assert.That(flipChangeLabel.name, Is.EqualTo("FlipChangeLabel"));
            Assert.That(flipChangeButton.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(116f));
            Assert.That(flipChangeButton.GetComponent<LayoutElement>().preferredHeight, Is.EqualTo(34f));
            Assert.That(resetButton.name, Is.EqualTo("ResetInput_New"));
            Assert.That(resetButton.transition, Is.EqualTo(Selectable.Transition.Animation));
            Assert.That(resetButton.animationTriggers.disabledTrigger, Is.EqualTo("Disabled"));
            Assert.That(resetButton.animationTriggers.disabledTrigger, Is.Not.EqualTo(resetButton.animationTriggers.pressedTrigger));
            Assert.That(resetButton.GetComponent<Animator>(), Is.Not.Null);
            Assert.That(resetButtonLabel.name, Is.EqualTo("ResetInputLabel"));
            Assert.That(resetButton.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(180f));
            Assert.That(resetButton.GetComponent<LayoutElement>().preferredHeight, Is.EqualTo(40f));
            Assert.That(inputView.transform.Find("FlipInputRow/FlipChange_Legacy").gameObject.activeSelf, Is.False);
            Assert.That(inputView.transform.Find("InputResetRow/ResetInput_Legacy").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SettingsInputViewRuntimeBind_UpdatesVisiblePushAndFlipKeyDisplays()
        {
            var parentObject = new GameObject("SettingsInputKeyDisplayRuntimeParent", typeof(RectTransform));
            var parentRect = (RectTransform)parentObject.transform;
            var settingsPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var settingsView = UnityEngine.Object.Instantiate(settingsPrefab, parentRect, false);

            try
            {
                var inputView = settingsView.InputView;
                var serializedInput = new SerializedObject(inputView);
                var pushKeyDisplayLabel = (TMP_Text)serializedInput.FindProperty("_pushKeyDisplayLabel").objectReferenceValue;
                var flipKeyDisplayLabel = (TMP_Text)serializedInput.FindProperty("_flipKeyDisplayLabel").objectReferenceValue;
                var viewModel = new SettingsInputViewModel();

                inputView.Bind(viewModel);
                viewModel.SetContent(
                    "Input",
                    "Movement Keys",
                    "Use Arrow Keys",
                    false,
                    "WASD",
                    "Push",
                    "R",
                    "Change",
                    "Flip",
                    "T",
                    "Change",
                    "Reset Input",
                    string.Empty,
                    false,
                    null,
                    true);

                Assert.That(pushKeyDisplayLabel.text, Is.EqualTo("R"));
                Assert.That(flipKeyDisplayLabel.text, Is.EqualTo("T"));

                viewModel.SetContent(
                    "Input",
                    "Movement Keys",
                    "Use Arrow Keys",
                    false,
                    "WASD",
                    "Push",
                    "E",
                    "Change",
                    "Flip",
                    "Q",
                    "Change",
                    "Reset Input",
                    string.Empty,
                    false,
                    null,
                    true);

                Assert.That(pushKeyDisplayLabel.text, Is.EqualTo("E"));
                Assert.That(flipKeyDisplayLabel.text, Is.EqualTo("Q"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingsView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void SettingsInputViewRuntimeBind_HighlightsOnlyActiveRebindButton()
        {
            var parentObject = new GameObject("SettingsInputRebindVisualRuntimeParent", typeof(RectTransform));
            var parentRect = (RectTransform)parentObject.transform;
            var settingsPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var settingsView = UnityEngine.Object.Instantiate(settingsPrefab, parentRect, false);

            try
            {
                var inputView = settingsView.InputView;
                var viewModel = new SettingsInputViewModel();

                inputView.Bind(viewModel);
                SetInputContent(viewModel, true, KeyboardBindableAction.Push, "Press a key for Push...");
                Assert.That(inputView.IsPushChangeInteractable, Is.True);
                Assert.That(inputView.IsFlipChangeInteractable, Is.False);
                Assert.That(inputView.IsResetInteractable, Is.False);

                SetInputContent(viewModel, true, KeyboardBindableAction.Flip, "Press a key for Flip...");
                Assert.That(inputView.IsPushChangeInteractable, Is.False);
                Assert.That(inputView.IsFlipChangeInteractable, Is.True);
                Assert.That(inputView.IsResetInteractable, Is.False);

                SetInputContent(viewModel, false, null, string.Empty);
                Assert.That(inputView.IsPushChangeInteractable, Is.True);
                Assert.That(inputView.IsFlipChangeInteractable, Is.True);
                Assert.That(inputView.IsResetInteractable, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingsView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }

            static void SetInputContent(
                SettingsInputViewModel viewModel,
                bool isRebinding,
                KeyboardBindableAction? rebindingAction,
                string statusText)
            {
                viewModel.SetContent(
                    "Input",
                    "Movement Keys",
                    "Use Arrow Keys",
                    false,
                    "WASD",
                    "Push",
                    "R",
                    "Change",
                    "Flip",
                    "T",
                    "Change",
                    "Reset Input",
                    statusText,
                    isRebinding,
                    rebindingAction,
                    !isRebinding);
            }
        }

        [TestCase(1920f, 1080f, 560f, 640f)]
        [TestCase(1280f, 720f, 560f, 592f)]
        [TestCase(1366f, 768f, 560f, 640f)]
        public void SettingsScreenRuntimeLayout_ClampsCenteredPanelWithinParent(
            float parentWidth,
            float parentHeight,
            float expectedWidth,
            float expectedHeight)
        {
            var parentObject = new GameObject("SettingsScreenRuntimeLayoutParent", typeof(RectTransform));
            var parentRect = (RectTransform)parentObject.transform;
            var settingsPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var settingsView = UnityEngine.Object.Instantiate(settingsPrefab, parentRect, false);

            try
            {
                parentRect.anchorMin = Vector2.zero;
                parentRect.anchorMax = Vector2.zero;
                parentRect.pivot = Vector2.zero;
                parentRect.sizeDelta = new Vector2(parentWidth, parentHeight);

                settingsView.SetIsCurrent(true);
                var settingsRect = (RectTransform)settingsView.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(settingsRect);

                Assert.That(settingsRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(settingsRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(settingsRect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(settingsRect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(settingsRect.sizeDelta.x, Is.EqualTo(expectedWidth).Within(0.01f));
                Assert.That(settingsRect.sizeDelta.y, Is.EqualTo(expectedHeight).Within(0.01f));
                Assert.That(settingsRect.sizeDelta.x, Is.LessThanOrEqualTo(parentWidth - 128f));
                Assert.That(settingsRect.sizeDelta.y, Is.LessThanOrEqualTo(parentHeight - 128f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingsView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void SettingsScreenRuntimeLayout_UsesSectionHostAndLayoutGroups()
        {
            var parentObject = new GameObject("SettingsScreenRuntimeLayoutSectionParent", typeof(RectTransform));
            var parentRect = (RectTransform)parentObject.transform;
            var settingsPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var settingsView = UnityEngine.Object.Instantiate(settingsPrefab, parentRect, false);

            try
            {
                parentRect.sizeDelta = new Vector2(1920f, 1080f);
                settingsView.SetIsCurrent(true);
                var settingsRect = (RectTransform)settingsView.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(settingsRect);

                var sectionHost = settingsView.transform.Find("SettingsSectionHost") as RectTransform;
                Assert.That(sectionHost, Is.Not.Null);
                Assert.That(settingsView.AudioView.transform.parent, Is.EqualTo(sectionHost));
                Assert.That(settingsView.DisplayView.transform.parent, Is.EqualTo(sectionHost));
                Assert.That(settingsView.InputView.transform.parent, Is.EqualTo(sectionHost));

                var audioRect = (RectTransform)settingsView.AudioView.transform;
                Assert.That(audioRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(audioRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(audioRect.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(settingsView.AudioView.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
                Assert.That(settingsView.AudioView.transform.Find("MainAudioRow").GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
                Assert.That(settingsView.DisplayView.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
                Assert.That(settingsView.DisplayView.transform.Find("ResolutionRow").GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
                Assert.That(settingsView.InputView.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
                Assert.That(settingsView.InputView.transform.Find("PushInputRow").GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingsView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void SettingsScreenRuntimeLayout_KeepsAuthoredBackgroundBehindContent()
        {
            var parentObject = new GameObject("SettingsScreenRuntimeLayoutBackgroundParent", typeof(RectTransform));
            var parentRect = (RectTransform)parentObject.transform;
            var settingsPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var settingsView = UnityEngine.Object.Instantiate(settingsPrefab, parentRect, false);

            try
            {
                parentRect.sizeDelta = new Vector2(1920f, 1080f);
                settingsView.SetIsCurrent(true);

                var root = settingsView.transform;
                Assert.That(root.GetChild(0).name, Is.EqualTo("Background"));
                Assert.That(root.Find("SettingsHeader").GetSiblingIndex(), Is.EqualTo(1));
                Assert.That(root.Find("SettingsTabRow").GetSiblingIndex(), Is.EqualTo(2));
                Assert.That(root.Find("SettingsSectionHost").GetSiblingIndex(), Is.EqualTo(3));
                Assert.That(root.Find("SettingsFooter").GetSiblingIndex(), Is.EqualTo(4));
                Assert.That(root.Find("SettingsAccessibilityRows").gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingsView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [TestCase(1920f, 1080f, 520f, 360f)]
        [TestCase(1280f, 720f, 520f, 360f)]
        [TestCase(1366f, 768f, 520f, 360f)]
        [TestCase(500f, 360f, 372f, 232f)]
        public void ObjectiveStatusScreenRuntimeLayout_ClampsTopCenterPanelWithinParent(
            float parentWidth,
            float parentHeight,
            float expectedWidth,
            float expectedHeight)
        {
            var parentObject = new GameObject("ObjectiveStatusScreenRuntimeLayoutParent", typeof(RectTransform));
            var parentRect = (RectTransform)parentObject.transform;
            parentRect.anchorMin = Vector2.zero;
            parentRect.anchorMax = Vector2.zero;
            parentRect.pivot = Vector2.zero;
            parentRect.sizeDelta = new Vector2(parentWidth, parentHeight);
            var objectivePrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<ObjectiveStatusScreenView>(UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath);
            var objectiveView = UnityEngine.Object.Instantiate(objectivePrefab, parentRect, false);

            try
            {
                objectiveView.Bind(new ObjectiveStatusScreenViewModel());
                objectiveView.SetIsCurrent(true);
                var objectiveRect = (RectTransform)objectiveView.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(objectiveRect);

                Assert.That(objectiveRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(objectiveRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(objectiveRect.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(objectiveRect.anchoredPosition, Is.EqualTo(new Vector2(0f, -20f)));
                Assert.That(objectiveRect.sizeDelta.x, Is.EqualTo(expectedWidth).Within(0.01f));
                Assert.That(objectiveRect.sizeDelta.y, Is.EqualTo(expectedHeight).Within(0.01f));
                Assert.That(objectiveRect.sizeDelta.x, Is.LessThanOrEqualTo(Mathf.Max(1f, parentWidth - 128f)));
                Assert.That(objectiveRect.sizeDelta.y, Is.LessThanOrEqualTo(Mathf.Max(1f, parentHeight - 128f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(objectiveView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void ObjectiveStatusScreenRuntimeLayout_UsesLayoutContainersAndFlexibleDetail()
        {
            var parentObject = new GameObject("ObjectiveStatusScreenRuntimeLayoutContainerParent", typeof(RectTransform));
            var parentRect = (RectTransform)parentObject.transform;
            parentRect.sizeDelta = new Vector2(1920f, 1080f);
            var objectivePrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<ObjectiveStatusScreenView>(UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath);
            var objectiveView = UnityEngine.Object.Instantiate(objectivePrefab, parentRect, false);

            try
            {
                objectiveView.Bind(new ObjectiveStatusScreenViewModel());
                objectiveView.SetIsCurrent(true);
                var objectiveRect = (RectTransform)objectiveView.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(objectiveRect);

                Assert.That(objectiveView.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
                Assert.That(objectiveView.GetComponent<LayoutElement>(), Is.Not.Null);

                var header = objectiveView.transform.Find("ObjectiveHeader") as RectTransform;
                var summary = objectiveView.transform.Find("ObjectiveSummary") as RectTransform;
                var detail = objectiveView.transform.Find("ObjectiveDetail") as RectTransform;
                var secondary = objectiveView.transform.Find("ObjectiveSecondary") as RectTransform;
                var footer = objectiveView.transform.Find("ObjectiveFooter") as RectTransform;

                Assert.That(header, Is.Not.Null);
                Assert.That(summary, Is.Not.Null);
                Assert.That(detail, Is.Not.Null);
                Assert.That(secondary, Is.Not.Null);
                Assert.That(footer, Is.Not.Null);
                Assert.That(header.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
                Assert.That(footer.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
                Assert.That(detail.GetComponent<LayoutElement>().flexibleHeight, Is.EqualTo(1f));
                Assert.That(objectiveView.transform.Find("ObjectiveHeader/Title"), Is.Not.Null);
                Assert.That(objectiveView.transform.Find("ObjectiveHeader/Badge"), Is.Not.Null);
                Assert.That(objectiveView.transform.Find("ObjectiveSummary/Summary"), Is.Not.Null);
                Assert.That(objectiveView.transform.Find("ObjectiveDetail/Detail"), Is.Not.Null);
                Assert.That(objectiveView.transform.Find("ObjectiveSecondary/Secondary"), Is.Not.Null);
                Assert.That(objectiveView.transform.Find("ObjectiveFooter/InfoButton"), Is.Not.Null);
                Assert.That(objectiveView.transform.Find("ObjectiveFooter/BackButton"), Is.Not.Null);
                Assert.That(objectiveView.transform.Find("ObjectiveSummary/Summary").GetComponent<TMP_Text>().textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(objectiveView.transform.Find("ObjectiveDetail/Detail").GetComponent<TMP_Text>().textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(objectiveView.transform.Find("ObjectiveSecondary/Secondary").GetComponent<TMP_Text>().textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(objectiveView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [TestCase(1920f, 1080f, 460f, 220f)]
        [TestCase(1280f, 720f, 460f, 220f)]
        [TestCase(1366f, 768f, 460f, 220f)]
        [TestCase(420f, 260f, 292f, 132f)]
        public void StageResultScreenRuntimeLayout_ClampsCenteredPanelWithinParent(
            float parentWidth,
            float parentHeight,
            float expectedWidth,
            float expectedHeight)
        {
            var parentObject = CreateSizedRectParent("StageResultScreenRuntimeLayoutParent", parentWidth, parentHeight);
            var stagePrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<StageResultScreenView>(UiTestPrefabAssetUtility.StageResultScreenPrefabPath);
            var stageView = UnityEngine.Object.Instantiate(stagePrefab, parentObject.transform, false);

            try
            {
                stageView.Bind(new StageResultScreenViewModel());
                stageView.SetIsCurrent(true);
                var stageRect = (RectTransform)stageView.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(stageRect);

                AssertCenteredTerminalPanel(stageRect, parentWidth, parentHeight, expectedWidth, expectedHeight);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void StageResultScreenRuntimeLayout_UsesLayoutContainersAndFlexibleDetail()
        {
            var parentObject = CreateSizedRectParent("StageResultScreenRuntimeLayoutContainerParent", 1920f, 1080f);
            var stagePrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<StageResultScreenView>(UiTestPrefabAssetUtility.StageResultScreenPrefabPath);
            var stageView = UnityEngine.Object.Instantiate(stagePrefab, parentObject.transform, false);

            try
            {
                stageView.Bind(new StageResultScreenViewModel());
                stageView.SetIsCurrent(true);
                var stageRect = (RectTransform)stageView.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(stageRect);

                AssertTerminalResultLayout(stageView.transform, "ResultSummary");
                Assert.That(stageView.transform.Find("ResultHeader/Title"), Is.Not.Null);
                Assert.That(stageView.transform.Find("ResultSummary/Summary"), Is.Not.Null);
                Assert.That(stageView.transform.Find("ResultDetail/Detail"), Is.Not.Null);
                Assert.That(stageView.transform.Find("ResultFooter/ContinueButton"), Is.Not.Null);
                Assert.That(stageView.transform.Find("ResultSummary/Summary").GetComponent<TMP_Text>().textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(stageView.transform.Find("ResultDetail/Detail").GetComponent<TMP_Text>().textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [TestCase(1920f, 1080f, 460f, 230f)]
        [TestCase(1280f, 720f, 460f, 230f)]
        [TestCase(1366f, 768f, 460f, 230f)]
        [TestCase(420f, 260f, 292f, 132f)]
        public void LevelFailedScreenRuntimeLayout_ClampsCenteredPanelWithinParent(
            float parentWidth,
            float parentHeight,
            float expectedWidth,
            float expectedHeight)
        {
            var parentObject = CreateSizedRectParent("LevelFailedScreenRuntimeLayoutParent", parentWidth, parentHeight);
            var levelFailedPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<LevelFailedScreenView>(UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath);
            var levelFailedView = UnityEngine.Object.Instantiate(levelFailedPrefab, parentObject.transform, false);

            try
            {
                levelFailedView.Bind(new LevelFailedScreenViewModel());
                levelFailedView.SetIsCurrent(true);
                var levelFailedRect = (RectTransform)levelFailedView.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(levelFailedRect);

                AssertCenteredTerminalPanel(levelFailedRect, parentWidth, parentHeight, expectedWidth, expectedHeight);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(levelFailedView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void LevelFailedScreenRuntimeLayout_UsesLayoutContainersAndFlexibleDetail()
        {
            var parentObject = CreateSizedRectParent("LevelFailedScreenRuntimeLayoutContainerParent", 1920f, 1080f);
            var levelFailedPrefab = UiTestPrefabAssetUtility.LoadScreenPrefab<LevelFailedScreenView>(UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath);
            var levelFailedView = UnityEngine.Object.Instantiate(levelFailedPrefab, parentObject.transform, false);

            try
            {
                levelFailedView.Bind(new LevelFailedScreenViewModel());
                levelFailedView.SetIsCurrent(true);
                var levelFailedRect = (RectTransform)levelFailedView.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(levelFailedRect);

                AssertTerminalResultLayout(levelFailedView.transform);
                Assert.That(levelFailedView.transform.Find("ResultHeader/Title"), Is.Not.Null);
                Assert.That(levelFailedView.transform.Find("ResultDetail/Detail"), Is.Not.Null);
                Assert.That(levelFailedView.transform.Find("ResultFooter/RestartLevelButton"), Is.Not.Null);
                Assert.That(levelFailedView.transform.Find("ResultFooter/MainButton"), Is.Not.Null);
                Assert.That(levelFailedView.transform.Find("ResultDetail/Detail").GetComponent<TMP_Text>().textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(levelFailedView.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void LevelFailedScreenRuntimeFallback_UsesLayoutContainersAndButtonRows()
        {
            var parentObject = CreateSizedRectParent("LevelFailedScreenRuntimeFallbackParent", 1920f, 1080f);
            var viewObject = new GameObject("LevelFailedScreenRuntimeFallback", typeof(RectTransform));
            viewObject.transform.SetParent(parentObject.transform, false);
            var levelFailedView = viewObject.AddComponent<LevelFailedScreenView>();

            try
            {
                levelFailedView.Bind(new LevelFailedScreenViewModel());
                levelFailedView.SetIsCurrent(true);
                var levelFailedRect = (RectTransform)levelFailedView.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(levelFailedRect);

                AssertCenteredTerminalPanel(levelFailedRect, 1920f, 1080f, 460f, 230f);
                AssertTerminalResultLayout(levelFailedView.transform);
                Assert.That(levelFailedView.transform.Find("ResultHeader/Title"), Is.Not.Null);
                Assert.That(levelFailedView.transform.Find("ResultDetail/Detail"), Is.Not.Null);
                Assert.That(levelFailedView.transform.Find("ResultFooter/RestartLevelButton"), Is.Not.Null);
                Assert.That(levelFailedView.transform.Find("ResultFooter/MainButton"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [TestCase(ScreenId.ObjectiveStatus)]
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

        [TestCase(ScreenId.ObjectiveStatus, "_screenPrefabCatalog.ObjectiveStatusPrefab", "CreateObjectiveStatusScreen(")]
        [TestCase(ScreenId.Settings, "_screenPrefabCatalog.SettingsPrefab", "CreateSettingsScreen(")]
        [TestCase(ScreenId.StageResult, "_screenPrefabCatalog.StageResultPrefab", "CreateStageResultScreen(")]
        [TestCase(ScreenId.LevelFailed, "_screenPrefabCatalog.LevelFailedPrefab", "CreateLevelFailedScreen(")]
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
        public void ScreenLegacyBuilderSymbols_AreAbsent_FromFactorySource_AndBaselineEvidence()
        {
            var installerSource = ReadRepoFile(InstallerSourcePath);
            var screenFactorySource = ReadRepoFile(ScreenFactorySourcePath);
            var baseline = ReadRepoFile(BaselineNotePath);

            Assert.That(installerSource, Does.Contain("_screenPrefabCatalog"));
            Assert.That(installerSource, Does.Not.Contain("ScreenPrefabRegistry"));
            Assert.That(screenFactorySource, Does.Not.Contain("Create" + "GameplayScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("Create" + "HelpScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateObjectiveStatusScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateSettingsScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateStageResultScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("CreateLevelFailedScreen("));
            Assert.That(screenFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreatePanel("));
            Assert.That(screenFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreateLabel("));
            Assert.That(screenFactorySource, Does.Not.Contain("UiCanvasElementFactory.CreateButton("));
            Assert.That(baseline, Does.Contain("screen legacy runtime builder paths were removed in the same phase"));
            Assert.That(baseline, Does.Contain("screen hybrid allowlist is now empty"));
            Assert.That(baseline, Does.Not.Contain("Screen:Gameplay -> GameplayScreenRuntimeFactory." + "Create" + "GameplayScreen"));
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
        public void PopupLegacyBuilderSymbols_AreAbsent_FromFactorySource_AndBaselineEvidence()
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

        private static void AssertSettingsSectionLayout(Transform sectionRoot)
        {
            RequireComponent<VerticalLayoutGroup>(sectionRoot);
            RequireComponent<LayoutElement>(sectionRoot);
        }

        private static void AssertObjectiveAssetLayout()
        {
            var objectiveRoot = UiTestPrefabAssetUtility
                .LoadScreenPrefab<ObjectiveStatusScreenView>(UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath)
                .transform;

            RequireComponent<VerticalLayoutGroup>(objectiveRoot);
            RequireComponent<LayoutElement>(objectiveRoot);
            RequireComponent<HorizontalLayoutGroup>(FindRequired(objectiveRoot, "ObjectiveHeader"));
            RequireComponent<LayoutElement>(FindRequired(objectiveRoot, "ObjectiveSummary"));
            RequireComponent<LayoutElement>(FindRequired(objectiveRoot, "ObjectiveDetail"));
            RequireComponent<LayoutElement>(FindRequired(objectiveRoot, "ObjectiveSecondary"));
            RequireComponent<HorizontalLayoutGroup>(FindRequired(objectiveRoot, "ObjectiveFooter"));
        }

        private static Transform FindRequired(Transform root, string path)
        {
            Assert.That(root, Is.Not.Null, path);
            var child = root.Find(path);
            Assert.That(child, Is.Not.Null, $"{root.name}/{path}");
            return child;
        }

        private static T RequireComponent<T>(Transform transform)
            where T : Component
        {
            Assert.That(transform, Is.Not.Null, typeof(T).Name);
            var component = transform.GetComponent<T>();
            Assert.That(component, Is.Not.Null, $"{typeof(T).Name} missing on {transform.name}");
            return component;
        }

        private static Component OpenMountedScreen(GameplayUiFlowInstaller installer, ScreenId screenId)
        {
            switch (screenId)
            {
                case ScreenId.ObjectiveStatus:
                    installer.Coordinator.OpenObjectiveStatusScreen();
                    return installer.ObjectiveStatusScreenView;

                case ScreenId.Settings:
                    installer.Coordinator.OpenSettingsScreen();
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
                    installer.Coordinator.OpenObjectiveStatusScreen();
                    installer.ObjectiveStatusScreenView.ClickInfo();
                    return installer.ObjectiveInfoPopupView;

                case PopupId.Confirm:
                    installer.Coordinator.RequestConfirmPopup(
                        new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false));
                    return installer.ConfirmPopupView;

                case PopupId.Tooltip:
                    installer.Coordinator.RequestTooltipPopup(new TooltipPopupPayload("Tip", "Tooltip body"));
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
                case ScreenId.ObjectiveStatus:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<ObjectiveStatusScreenView>(UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath);

                case ScreenId.Settings:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);

                case ScreenId.StageResult:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<StageResultScreenView>(UiTestPrefabAssetUtility.StageResultScreenPrefabPath);

                case ScreenId.LevelFailed:
                    return UiTestPrefabAssetUtility.LoadScreenPrefab<LevelFailedScreenView>(UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath);

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
                typeof(ObjectiveStatusScreenView),
                typeof(SettingsScreenView),
                typeof(SettingsAudioView),
                typeof(SettingsDisplayView),
                typeof(StageResultScreenView),
                typeof(LevelFailedScreenView),
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
                typeof(ObjectiveStatusScreenView),
                typeof(SettingsScreenView),
                typeof(SettingsAudioView),
                typeof(SettingsDisplayView),
                typeof(StageResultScreenView),
                typeof(LevelFailedScreenView),
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

        private static GameObject CreateSizedRectParent(string objectName, float width, float height)
        {
            var parentObject = new GameObject(objectName, typeof(RectTransform));
            var parentRect = (RectTransform)parentObject.transform;
            parentRect.anchorMin = Vector2.zero;
            parentRect.anchorMax = Vector2.zero;
            parentRect.pivot = Vector2.zero;
            parentRect.sizeDelta = new Vector2(width, height);
            return parentObject;
        }

        private static void AssertCenteredTerminalPanel(
            RectTransform rectTransform,
            float parentWidth,
            float parentHeight,
            float expectedWidth,
            float expectedHeight)
        {
            Assert.That(rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rectTransform.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rectTransform.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(rectTransform.sizeDelta.x, Is.EqualTo(expectedWidth).Within(0.01f));
            Assert.That(rectTransform.sizeDelta.y, Is.EqualTo(expectedHeight).Within(0.01f));
            Assert.That(rectTransform.sizeDelta.x, Is.LessThanOrEqualTo(Mathf.Max(1f, parentWidth - 128f)));
            Assert.That(rectTransform.sizeDelta.y, Is.LessThanOrEqualTo(Mathf.Max(1f, parentHeight - 128f)));
        }

        private static void AssertTerminalResultLayout(Transform root, string optionalMiddleContainerName = null)
        {
            Assert.That(root.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(root.GetComponent<LayoutElement>(), Is.Not.Null);

            var header = root.Find("ResultHeader") as RectTransform;
            var detail = root.Find("ResultDetail") as RectTransform;
            var footer = root.Find("ResultFooter") as RectTransform;

            Assert.That(header, Is.Not.Null);
            Assert.That(detail, Is.Not.Null);
            Assert.That(footer, Is.Not.Null);
            Assert.That(detail.GetComponent<LayoutElement>().flexibleHeight, Is.EqualTo(1f));
            Assert.That(footer.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);

            if (!string.IsNullOrEmpty(optionalMiddleContainerName))
            {
                Assert.That(root.Find(optionalMiddleContainerName), Is.Not.Null);
            }
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

        private static void AssertSerializedButtonInactive(SerializedObject serializedObject, string propertyPath)
        {
            var property = serializedObject.FindProperty(propertyPath);
            Assert.That(property, Is.Not.Null, propertyPath);
            Assert.That(property.objectReferenceValue, Is.InstanceOf<Button>(), propertyPath);

            var button = (Button)property.objectReferenceValue;
            Assert.That(button.gameObject.activeSelf, Is.False, propertyPath);
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
