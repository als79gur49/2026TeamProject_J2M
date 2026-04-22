using System;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Feature.UI.Editor
{
    public static class ScreenPrefabMigrationAuthoring
    {
        private const string PrefabDirectoryPath = "Assets/_Features/UI/UI_Screens/Prefabs";
        private const string GameplayScreenPrefabPath = PrefabDirectoryPath + "/GameplayScreen.prefab";
        private const string HelpScreenPrefabPath = PrefabDirectoryPath + "/HelpScreen.prefab";
        private const string ObjectiveStatusScreenPrefabPath = PrefabDirectoryPath + "/ObjectiveStatusScreen.prefab";
        private const string InventoryScreenPrefabPath = PrefabDirectoryPath + "/InventoryScreen.prefab";
        private const string SettingsScreenPrefabPath = PrefabDirectoryPath + "/SettingsScreen.prefab";
        private const string StageResultScreenPrefabPath = PrefabDirectoryPath + "/StageResultScreen.prefab";
        private const string ScreenCatalogPath = PrefabDirectoryPath + "/GameplayScreenPrefabCatalog.asset";
        private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";

        [MenuItem("Game/UI/Rebuild Canonical Screen Prefabs")]
        public static void RebuildCanonicalScreenPrefabs()
        {
            EnsureFolderExists(PrefabDirectoryPath);

            SavePrefab(BuildGameplayScreenPrefab(), GameplayScreenPrefabPath);
            SavePrefab(BuildHelpScreenPrefab(), HelpScreenPrefabPath);
            SavePrefab(BuildObjectiveStatusScreenPrefab(), ObjectiveStatusScreenPrefabPath);
            SavePrefab(BuildInventoryScreenPrefab(), InventoryScreenPrefabPath);
            SavePrefab(BuildSettingsScreenPrefab(), SettingsScreenPrefabPath);
            SavePrefab(BuildStageResultScreenPrefab(), StageResultScreenPrefabPath);

            ImportPrefabAssets();

            var catalog = LoadOrCreateCatalog();
            SetField(catalog, "_gameplayPrefab", LoadPrefabComponent<GameplayScreenView>(GameplayScreenPrefabPath));
            SetField(catalog, "_helpPrefab", LoadPrefabComponent<HelpScreenView>(HelpScreenPrefabPath));
            SetField(catalog, "_objectiveStatusPrefab", LoadPrefabComponent<ObjectiveStatusScreenView>(ObjectiveStatusScreenPrefabPath));
            SetField(catalog, "_inventoryPrefab", LoadPrefabComponent<InventoryScreenView>(InventoryScreenPrefabPath));
            SetField(catalog, "_settingsPrefab", LoadPrefabComponent<SettingsScreenView>(SettingsScreenPrefabPath));
            SetField(catalog, "_stageResultPrefab", LoadPrefabComponent<StageResultScreenView>(StageResultScreenPrefabPath));
            EditorUtility.SetDirty(catalog);

            WireTutorialScene(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Game/UI/Rebuild Settings Screen Prefab")]
        public static void RebuildSettingsScreenPrefabOnly()
        {
            EnsureFolderExists(PrefabDirectoryPath);
            SavePrefab(BuildSettingsScreenPrefab(), SettingsScreenPrefabPath);
            AssetDatabase.ImportAsset(SettingsScreenPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static GameObject BuildGameplayScreenPrefab()
        {
            var root = CreateScreenPanel<GameplayScreenView>(
                "GameplayScreen",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(360f, 118f),
                new Vector2(16f, -16f),
                out var view);
            var rootRect = root.GetComponent<RectTransform>();

            var title = CreateLabel("Title", rootRect, new Vector2(12f, -12f), new Vector2(336f, 24f), TextAnchor.MiddleLeft, 18);
            var help = CreateButton("HelpButton", rootRect, "Help", new Vector2(12f, -76f), new Vector2(80f, 28f));
            var objective = CreateButton("ObjectiveButton", rootRect, "Objectives", new Vector2(102f, -76f), new Vector2(96f, 28f));
            var inventory = CreateButton("InventoryButton", rootRect, "Inventory", new Vector2(208f, -76f), new Vector2(80f, 28f));
            var settings = CreateButton("SettingsButton", rootRect, "Settings", new Vector2(298f, -76f), new Vector2(80f, 28f));

            SetField(view, "_root", root);
            SetField(view, "_helpButton", help.Button);
            SetField(view, "_objectiveButton", objective.Button);
            SetField(view, "_inventoryButton", inventory.Button);
            SetField(view, "_settingsButton", settings.Button);
            SetField(view, "_titleLabel", title);
            SetField(view, "_helpButtonLabel", help.Label);
            SetField(view, "_objectiveButtonLabel", objective.Label);
            SetField(view, "_inventoryButtonLabel", inventory.Label);
            SetField(view, "_settingsButtonLabel", settings.Label);
            return root;
        }

        private static GameObject BuildHelpScreenPrefab()
        {
            var root = CreateScreenPanel<HelpScreenView>(
                "HelpScreen",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(360f, 170f),
                new Vector2(0f, -20f),
                out var view);
            var rootRect = root.GetComponent<RectTransform>();

            var title = CreateLabel("Title", rootRect, new Vector2(16f, -16f), new Vector2(328f, 24f), TextAnchor.MiddleCenter, 18);
            var description = CreateLabel("Description", rootRect, new Vector2(16f, -54f), new Vector2(328f, 48f), TextAnchor.UpperCenter, 15);
            var back = CreateButton("BackButton", rootRect, "Back", new Vector2(131f, -126f), new Vector2(98f, 28f));

            SetField(view, "_root", root);
            SetField(view, "_titleLabel", title);
            SetField(view, "_descriptionLabel", description);
            SetField(view, "_backButton", back.Button);
            SetField(view, "_backButtonLabel", back.Label);
            return root;
        }

        private static GameObject BuildObjectiveStatusScreenPrefab()
        {
            var root = CreateScreenPanel<ObjectiveStatusScreenView>(
                "ObjectiveStatusScreen",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(440f, 250f),
                new Vector2(0f, -20f),
                out var view);
            var rootRect = root.GetComponent<RectTransform>();

            var title = CreateLabel("Title", rootRect, new Vector2(16f, -16f), new Vector2(408f, 24f), TextAnchor.MiddleCenter, 18);
            var badge = CreateLabel("Badge", rootRect, new Vector2(16f, -48f), new Vector2(408f, 22f), TextAnchor.MiddleCenter, 16);
            var summary = CreateLabel("Summary", rootRect, new Vector2(16f, -78f), new Vector2(408f, 52f), TextAnchor.UpperLeft, 15);
            var detail = CreateLabel("Detail", rootRect, new Vector2(16f, -138f), new Vector2(408f, 22f), TextAnchor.MiddleLeft, 14);
            var secondary = CreateLabel("Secondary", rootRect, new Vector2(16f, -164f), new Vector2(408f, 36f), TextAnchor.UpperLeft, 14);
            var overview = CreateButton("OverviewButton", rootRect, "Overview", new Vector2(16f, -216f), new Vector2(92f, 28f));
            var session = CreateButton("SessionButton", rootRect, "Session", new Vector2(116f, -216f), new Vector2(92f, 28f));
            var info = CreateButton("InfoButton", rootRect, "Info", new Vector2(216f, -216f), new Vector2(92f, 28f));
            var back = CreateButton("BackButton", rootRect, "Back", new Vector2(316f, -216f), new Vector2(92f, 28f));

            SetField(view, "_root", root);
            SetField(view, "_titleLabel", title);
            SetField(view, "_badgeLabel", badge);
            SetField(view, "_summaryLabel", summary);
            SetField(view, "_detailLabel", detail);
            SetField(view, "_secondaryLabel", secondary);
            SetField(view, "_overviewButton", overview.Button);
            SetField(view, "_sessionButton", session.Button);
            SetField(view, "_infoButton", info.Button);
            SetField(view, "_backButton", back.Button);
            return root;
        }

        private static GameObject BuildInventoryScreenPrefab()
        {
            var root = CreateScreenPanel<InventoryScreenView>(
                "InventoryScreen",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(584f, 330f),
                new Vector2(0f, -20f),
                out var view);
            var rootRect = root.GetComponent<RectTransform>();

            var title = CreateLabel("Title", rootRect, new Vector2(16f, -16f), new Vector2(552f, 24f), TextAnchor.MiddleCenter, 18);

            var catalogPanel = CreatePanel(
                "CatalogPanel",
                rootRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(248f, 220f),
                new Vector2(16f, -52f));
            var catalogView = catalogPanel.gameObject.AddComponent<InventoryCatalogView>();
            var search = CreateButton("SearchButton", catalogPanel, "Search", new Vector2(8f, -8f), new Vector2(72f, 24f));
            var filter = CreateButton("FilterButton", catalogPanel, "Filter", new Vector2(86f, -8f), new Vector2(74f, 24f));
            var sort = CreateButton("SortButton", catalogPanel, "Sort", new Vector2(166f, -8f), new Vector2(74f, 24f));
            var summary = CreateLabel("Summary", catalogPanel, new Vector2(8f, -38f), new Vector2(232f, 18f), TextAnchor.MiddleLeft, 13);
            var empty = CreateLabel("Empty", catalogPanel, new Vector2(8f, -188f), new Vector2(232f, 24f), TextAnchor.UpperLeft, 12);
            var rowButtons = new Button[5];
            var rowLabels = new Text[5];
            var rowMeta = new Text[5];
            for (var i = 0; i < rowButtons.Length; i++)
            {
                var row = CreateButton($"RowButton{i}", catalogPanel, $"Row {i + 1}", new Vector2(8f, -64f - (i * 26f)), new Vector2(122f, 24f));
                row.Label.alignment = TextAnchor.MiddleLeft;
                rowButtons[i] = row.Button;
                rowLabels[i] = row.Label;
                rowMeta[i] = CreateLabel($"RowMeta{i}", catalogPanel, new Vector2(136f, -64f - (i * 26f)), new Vector2(104f, 24f), TextAnchor.MiddleLeft, 12);
            }

            var detailPanel = CreatePanel(
                "DetailPanel",
                rootRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(288f, 120f),
                new Vector2(280f, -52f));
            var detailView = detailPanel.gameObject.AddComponent<InventoryDetailView>();
            var detailTitle = CreateLabel("DetailTitle", detailPanel, new Vector2(12f, -10f), new Vector2(264f, 22f), TextAnchor.MiddleLeft, 16);
            var detailBadge = CreateLabel("DetailBadge", detailPanel, new Vector2(12f, -36f), new Vector2(264f, 18f), TextAnchor.MiddleLeft, 13);
            var detailDescription = CreateLabel("DetailDescription", detailPanel, new Vector2(12f, -58f), new Vector2(264f, 34f), TextAnchor.UpperLeft, 13);
            var detailBody = CreateLabel("DetailBody", detailPanel, new Vector2(12f, -92f), new Vector2(264f, 24f), TextAnchor.UpperLeft, 12);

            var actionPanel = CreatePanel(
                "ActionPanel",
                rootRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(288f, 112f),
                new Vector2(280f, -180f));
            var actionView = actionPanel.gameObject.AddComponent<InventoryActionView>();
            var primary = CreateButton("PrimaryActionButton", actionPanel, "Primary", new Vector2(12f, -12f), new Vector2(110f, 26f));
            var secondary = CreateButton("SecondaryActionButton", actionPanel, "Secondary", new Vector2(12f, -46f), new Vector2(110f, 26f));
            var primaryState = CreateLabel("PrimaryState", actionPanel, new Vector2(130f, -12f), new Vector2(146f, 24f), TextAnchor.MiddleLeft, 12);
            var secondaryState = CreateLabel("SecondaryState", actionPanel, new Vector2(130f, -46f), new Vector2(146f, 24f), TextAnchor.MiddleLeft, 12);
            var feedback = CreateLabel("ActionFeedback", actionPanel, new Vector2(12f, -78f), new Vector2(264f, 24f), TextAnchor.UpperLeft, 12);

            var back = CreateButton("BackButton", rootRect, "Back", new Vector2(243f, -290f), new Vector2(98f, 28f));

            SetField(view, "_root", root);
            SetField(view, "_titleLabel", title);
            SetField(view, "_catalogView", catalogView);
            SetField(view, "_detailView", detailView);
            SetField(view, "_actionView", actionView);
            SetField(view, "_backButton", back.Button);
            SetField(view, "_backButtonLabel", back.Label);

            SetField(catalogView, "_searchButton", search.Button);
            SetField(catalogView, "_filterButton", filter.Button);
            SetField(catalogView, "_sortButton", sort.Button);
            SetField(catalogView, "_searchButtonLabel", search.Label);
            SetField(catalogView, "_filterButtonLabel", filter.Label);
            SetField(catalogView, "_sortButtonLabel", sort.Label);
            SetField(catalogView, "_summaryLabel", summary);
            SetField(catalogView, "_emptyStateLabel", empty);
            SetField(catalogView, "_rowButtons", rowButtons);
            SetField(catalogView, "_rowLabelTexts", rowLabels);
            SetField(catalogView, "_rowMetaTexts", rowMeta);

            SetField(detailView, "_titleLabel", detailTitle);
            SetField(detailView, "_badgeLabel", detailBadge);
            SetField(detailView, "_descriptionLabel", detailDescription);
            SetField(detailView, "_detailLabel", detailBody);

            SetField(actionView, "_primaryButton", primary.Button);
            SetField(actionView, "_secondaryButton", secondary.Button);
            SetField(actionView, "_primaryButtonLabel", primary.Label);
            SetField(actionView, "_secondaryButtonLabel", secondary.Label);
            SetField(actionView, "_primaryStateLabel", primaryState);
            SetField(actionView, "_secondaryStateLabel", secondaryState);
            SetField(actionView, "_feedbackLabel", feedback);
            return root;
        }

        private static GameObject BuildSettingsScreenPrefab()
        {
            var root = CreateScreenPanel<SettingsScreenView>(
                "SettingsScreen",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(460f, 600f),
                new Vector2(0f, -20f),
                out var view);
            var rootRect = root.GetComponent<RectTransform>();

            var title = CreateLabel("Title", rootRect, new Vector2(16f, -16f), new Vector2(428f, 24f), TextAnchor.MiddleCenter, 18);

            var audioSection = CreatePanel(
                SettingsScreenView.AudioSectionName,
                rootRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(412f, 124f),
                new Vector2(24f, -58f));
            var audioView = audioSection.gameObject.AddComponent<SettingsAudioView>();
            var mainRow = CreateAudioRow("MainAudioRow", audioSection, new Vector2(0f, 0f));
            var bgmRow = CreateAudioRow("BgmAudioRow", audioSection, new Vector2(0f, -46f));
            var sfxRow = CreateAudioRow("SfxAudioRow", audioSection, new Vector2(0f, -92f));

            var displaySection = CreatePanel(
                SettingsScreenView.DisplaySectionName,
                rootRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(412f, 248f),
                new Vector2(24f, -194f));
            var displayView = displaySection.gameObject.AddComponent<SettingsDisplayView>();
            var displaySectionTitle = CreateLabel("DisplaySectionTitle", displaySection, new Vector2(0f, 0f), new Vector2(160f, 22f), TextAnchor.MiddleLeft, 15);
            var currentDisplayLabel = CreateLabel("CurrentDisplayLabel", displaySection, new Vector2(0f, -32f), new Vector2(120f, 22f), TextAnchor.MiddleLeft, 14);
            var currentDisplayValue = CreateLabel("CurrentDisplayValue", displaySection, new Vector2(132f, -32f), new Vector2(256f, 22f), TextAnchor.MiddleLeft, 14);
            var resolutionLabel = CreateLabel("ResolutionLabel", displaySection, new Vector2(0f, -70f), new Vector2(120f, 22f), TextAnchor.MiddleLeft, 14);
            var resolutionDropdown = CreateResolutionDropdown("ResolutionDropdown", displaySection, new Vector2(132f, -64f), new Vector2(204f, 30f));
            var resolutionInfoHotspot = CreateHoverHotspot("ResolutionInfoHotspot", displaySection, "i", new Vector2(344f, -64f), new Vector2(24f, 30f));
            var resolutionHoverHint = CreateHoverHintPanel(
                "ResolutionHoverHint",
                displaySection,
                new Vector2(140f, -8f),
                new Vector2(248f, 48f),
                "Only automatically detected resolutions are shown.");
            var fullscreenLabel = CreateLabel("FullscreenLabel", displaySection, new Vector2(0f, -110f), new Vector2(160f, 22f), TextAnchor.MiddleLeft, 14);
            var fullscreenToggle = CreateStandaloneToggle("FullscreenToggle", displaySection, "On", new Vector2(196f, -104f), new Vector2(140f, 28f));
            var displayStatus = CreateLabel("DisplayStatus", displaySection, new Vector2(0f, -152f), new Vector2(388f, 24f), TextAnchor.UpperLeft, 12);
            var previewCountdown = CreateCountdownStrip("DisplayPreviewCountdown", displaySection, new Vector2(0f, -182f), new Vector2(388f, 14f));
            var applyButton = CreateButton("DisplayApplyButton", displaySection, "Apply", new Vector2(74f, -208f), new Vector2(104f, 30f));
            var revertButton = CreateButton("DisplayRevertButton", displaySection, "Revert", new Vector2(220f, -208f), new Vector2(104f, 30f));

            var tooltipStatus = CreateLabel("TooltipStatus", rootRect, new Vector2(24f, -458f), new Vector2(160f, 22f), TextAnchor.MiddleLeft, 15);
            var tooltipInfo = CreateButton("TooltipInfo", rootRect, "i", new Vector2(188f, -452f), new Vector2(24f, 28f));
            var tooltipToggle = CreateButton("TooltipToggle", rootRect, "Toggle Tooltips", new Vector2(220f, -452f), new Vector2(140f, 28f));
            var largeTextStatus = CreateLabel("LargeTextStatus", rootRect, new Vector2(24f, -504f), new Vector2(160f, 22f), TextAnchor.MiddleLeft, 15);
            var largeTextToggle = CreateButton("LargeTextToggle", rootRect, "Toggle Large Text", new Vector2(220f, -498f), new Vector2(140f, 28f));
            var back = CreateButton("BackButton", rootRect, "Back", new Vector2(181f, -554f), new Vector2(98f, 30f));

            SetField(view, "_root", root);
            SetField(view, "_titleLabel", title);
            SetField(view, "_tooltipStatusLabel", tooltipStatus);
            SetField(view, "_largeTextStatusLabel", largeTextStatus);
            SetField(view, "_tooltipInfoButton", tooltipInfo.Button);
            SetField(view, "_tooltipToggleButton", tooltipToggle.Button);
            SetField(view, "_largeTextToggleButton", largeTextToggle.Button);
            SetField(view, "_backButton", back.Button);
            SetField(view, "_tooltipToggleButtonLabel", tooltipToggle.Label);
            SetField(view, "_largeTextToggleButtonLabel", largeTextToggle.Label);
            SetField(view, "_backButtonLabel", back.Label);
            SetField(view, "_audioView", audioView);
            SetField(view, "_displayView", displayView);

            SetField(audioView, "_mainRow", CreateAudioRowRefs(mainRow));
            SetField(audioView, "_bgmRow", CreateAudioRowRefs(bgmRow));
            SetField(audioView, "_sfxRow", CreateAudioRowRefs(sfxRow));

            SetField(displayView, "_sectionTitle", displaySectionTitle);
            SetField(displayView, "_currentDisplayLabel", currentDisplayLabel);
            SetField(displayView, "_currentDisplayValue", currentDisplayValue);
            SetField(displayView, "_resolutionLabel", resolutionLabel);
            SetField(displayView, "_resolutionDropdown", resolutionDropdown);
            SetField(displayView, "_resolutionInfoHotspot", resolutionInfoHotspot.Root);
            SetField(displayView, "_resolutionHoverRelay", resolutionInfoHotspot.Relay);
            SetField(displayView, "_resolutionHoverHintRoot", resolutionHoverHint.Panel);
            SetField(displayView, "_resolutionHoverHintLabel", resolutionHoverHint.Label);
            SetField(displayView, "_fullscreenLabel", fullscreenLabel);
            SetField(displayView, "_fullscreenToggle", fullscreenToggle.Toggle);
            SetField(displayView, "_displayStatusLabel", displayStatus);
            SetField(displayView, "_previewCountdownRoot", previewCountdown.Root);
            SetField(displayView, "_previewCountdownLabel", previewCountdown.Label);
            SetField(displayView, "_previewCountdownFill", previewCountdown.Fill);
            SetField(displayView, "_applyButton", applyButton.Button);
            SetField(displayView, "_applyButtonLabel", applyButton.Label);
            SetField(displayView, "_revertButton", revertButton.Button);
            SetField(displayView, "_revertButtonLabel", revertButton.Label);
            return root;
        }

        private static GameObject BuildStageResultScreenPrefab()
        {
            var root = CreateScreenPanel<StageResultScreenView>(
                "StageResultScreen",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(460f, 220f),
                Vector2.zero,
                out var view);
            var rootRect = root.GetComponent<RectTransform>();

            var title = CreateLabel("Title", rootRect, new Vector2(24f, -20f), new Vector2(412f, 30f), TextAnchor.MiddleCenter, 20, FontStyle.Bold);
            var summary = CreateLabel("Summary", rootRect, new Vector2(24f, -70f), new Vector2(412f, 30f), TextAnchor.MiddleCenter, 16);
            var detail = CreateLabel("Detail", rootRect, new Vector2(24f, -112f), new Vector2(412f, 44f), TextAnchor.UpperCenter, 14);
            var button = CreateButton("ContinueButton", rootRect, "Continue", new Vector2(181f, -176f), new Vector2(98f, 30f));

            SetField(view, "_root", root);
            SetField(view, "_titleLabel", title);
            SetField(view, "_summaryLabel", summary);
            SetField(view, "_detailLabel", detail);
            SetField(view, "_continueButton", button.Button);
            SetField(view, "_continueButtonLabel", button.Label);
            return root;
        }

        private static ScreenPrefabCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ScreenPrefabCatalog>(ScreenCatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<ScreenPrefabCatalog>();
            AssetDatabase.CreateAsset(catalog, ScreenCatalogPath);
            return catalog;
        }

        private static TView LoadPrefabComponent<TView>(string assetPath) where TView : Component
        {
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabRoot == null)
            {
                throw new InvalidOperationException($"Prefab asset missing at '{assetPath}'.");
            }

            var component = prefabRoot.GetComponent<TView>();
            if (component == null)
            {
                throw new InvalidOperationException($"Prefab at '{assetPath}' is missing '{typeof(TView).Name}'.");
            }

            return component;
        }

        private static void WireTutorialScene(ScreenPrefabCatalog catalog)
        {
            var scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
            var installer = FindInstaller(scene);
            if (installer == null)
            {
                throw new InvalidOperationException("TutorialScene is missing GameplayUiFlowInstaller.");
            }

            SetField(installer, "_screenPrefabCatalog", catalog);
            EditorUtility.SetDirty(installer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameplayUiFlowInstaller FindInstaller(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var installer = roots[i].GetComponentInChildren<GameplayUiFlowInstaller>(true);
                if (installer != null)
                {
                    return installer;
                }
            }

            return null;
        }

        private static GameObject CreateScreenPanel<TView>(
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta,
            Vector2 anchoredPosition,
            out TView view)
            where TView : Component
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var image = root.GetComponent<Image>();
            image.color = new Color(0.11f, 0.12f, 0.16f, 0.92f);

            view = root.AddComponent<TView>();
            return root;
        }

        private static RectTransform CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rectTransform = panel.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var image = panel.GetComponent<Image>();
            image.color = new Color(0.11f, 0.12f, 0.16f, 0.92f);
            return rectTransform;
        }

        private static Text CreateLabel(
            string name,
            RectTransform parent,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            TextAnchor alignment,
            int fontSize,
            FontStyle fontStyle = FontStyle.Normal)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var text = labelObject.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static ButtonParts CreateButton(
            string name,
            RectTransform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.20f, 0.25f, 0.34f, 1f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect);

            var text = labelObject.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            return new ButtonParts(button, text);
        }

        private static AudioRowParts CreateAudioRow(string name, RectTransform parent, Vector2 anchoredPosition)
        {
            var rowObject = new GameObject(name, typeof(RectTransform));
            rowObject.transform.SetParent(parent, false);

            var rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.sizeDelta = new Vector2(412f, 32f);
            rowRect.anchoredPosition = anchoredPosition;

            var rowLabel = CreateLabel("Label", rowRect, Vector2.zero, new Vector2(116f, 24f), TextAnchor.MiddleLeft, 14);
            var sliderParts = CreateAudioSlider(rowRect, new Vector2(126f, -2f), new Vector2(172f, 20f));
            var valueLabel = CreateLabel("Value", rowRect, new Vector2(306f, 0f), new Vector2(54f, 24f), TextAnchor.MiddleCenter, 13);
            var muteToggle = CreateMuteToggle("MuteToggle", rowRect, new Vector2(362f, -2f), new Vector2(50f, 24f));

            return new AudioRowParts(rowRect, rowLabel, valueLabel, sliderParts.Slider, muteToggle.Toggle, sliderParts.Relay);
        }

        private static SliderParts CreateAudioSlider(RectTransform parent, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);

            var sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 1f);
            sliderRect.anchorMax = new Vector2(0f, 1f);
            sliderRect.pivot = new Vector2(0f, 1f);
            sliderRect.anchoredPosition = anchoredPosition;
            sliderRect.sizeDelta = sizeDelta;

            var sliderBackground = sliderObject.GetComponent<Image>();
            sliderBackground.color = new Color(0.12f, 0.16f, 0.21f, 1f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(8f, 6f);
            fillAreaRect.offsetMax = new Vector2(-8f, -6f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.24f, 0.68f, 0.87f, 1f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(8f, 0f);
            handleAreaRect.offsetMax = new Vector2(-8f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image), typeof(SettingsSliderInteractionRelay));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(12f, 20f);
            handle.GetComponent<Image>().color = Color.white;

            var slider = sliderObject.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.value = 1f;

            return new SliderParts(slider, handle.GetComponent<SettingsSliderInteractionRelay>());
        }

        private static ToggleParts CreateMuteToggle(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var toggleObject = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);

            var toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 1f);
            toggleRect.anchorMax = new Vector2(0f, 1f);
            toggleRect.pivot = new Vector2(0f, 1f);
            toggleRect.anchoredPosition = anchoredPosition;
            toggleRect.sizeDelta = sizeDelta;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(toggleObject.transform, false);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(18f, 18f);
            var backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.20f, 0.25f, 0.34f, 1f);

            var checkmarkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmarkObject.transform.SetParent(backgroundObject.transform, false);
            var checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;
            var checkmarkImage = checkmarkObject.GetComponent<Image>();
            checkmarkImage.color = new Color(0.24f, 0.68f, 0.87f, 1f);

            var label = CreateLabel("Label", toggleRect, new Vector2(22f, 0f), new Vector2(28f, 24f), TextAnchor.MiddleLeft, 12);
            label.text = "Mute";

            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;

            return new ToggleParts(toggle);
        }

        private static Dropdown CreateResolutionDropdown(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var dropdownObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown));
            dropdownObject.transform.SetParent(parent, false);

            var dropdownRect = dropdownObject.GetComponent<RectTransform>();
            dropdownRect.anchorMin = new Vector2(0f, 1f);
            dropdownRect.anchorMax = new Vector2(0f, 1f);
            dropdownRect.pivot = new Vector2(0f, 1f);
            dropdownRect.anchoredPosition = anchoredPosition;
            dropdownRect.sizeDelta = sizeDelta;

            var backgroundImage = dropdownObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.14f, 0.18f, 0.26f, 1f);

            var caption = CreateLabel("Label", dropdownRect, new Vector2(8f, -4f), new Vector2(160f, 22f), TextAnchor.MiddleLeft, 13);
            var arrow = CreateLabel("Arrow", dropdownRect, new Vector2(172f, -4f), new Vector2(24f, 22f), TextAnchor.MiddleCenter, 14);
            arrow.text = "v";

            var templateObject = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateObject.transform.SetParent(dropdownRect, false);
            templateObject.SetActive(false);

            var templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 1f);
            templateRect.anchorMax = new Vector2(1f, 1f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -32f);
            templateRect.sizeDelta = new Vector2(0f, 120f);
            templateObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 1f);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(templateRect, false);
            var viewportRect = viewportObject.GetComponent<RectTransform>();
            Stretch(viewportRect);
            viewportObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 0.98f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportRect, false);
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 24f);

            var itemObject = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemObject.transform.SetParent(contentRect, false);
            var itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 24f);

            var itemBackgroundObject = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBackgroundObject.transform.SetParent(itemRect, false);
            var itemBackgroundRect = itemBackgroundObject.GetComponent<RectTransform>();
            Stretch(itemBackgroundRect);
            var itemBackgroundImage = itemBackgroundObject.GetComponent<Image>();
            itemBackgroundImage.color = new Color(0.16f, 0.21f, 0.30f, 1f);

            var itemCheckmarkObject = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            itemCheckmarkObject.transform.SetParent(itemRect, false);
            var itemCheckmarkRect = itemCheckmarkObject.GetComponent<RectTransform>();
            itemCheckmarkRect.anchorMin = new Vector2(0f, 0.5f);
            itemCheckmarkRect.anchorMax = new Vector2(0f, 0.5f);
            itemCheckmarkRect.pivot = new Vector2(0f, 0.5f);
            itemCheckmarkRect.anchoredPosition = new Vector2(8f, 0f);
            itemCheckmarkRect.sizeDelta = new Vector2(16f, 16f);
            var itemCheckmarkImage = itemCheckmarkObject.GetComponent<Image>();
            itemCheckmarkImage.color = new Color(0.24f, 0.68f, 0.87f, 1f);

            var itemLabel = CreateLabel("Item Label", itemRect, new Vector2(30f, -2f), new Vector2(160f, 22f), TextAnchor.MiddleLeft, 13);

            var itemToggle = itemObject.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBackgroundImage;
            itemToggle.graphic = itemCheckmarkImage;

            var scrollRect = templateObject.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.targetGraphic = backgroundImage;
            dropdown.template = templateRect;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;

            return dropdown;
        }

        private static ToggleParts CreateStandaloneToggle(string name, RectTransform parent, string stateLabelText, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var toggleObject = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);

            var toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 1f);
            toggleRect.anchorMax = new Vector2(0f, 1f);
            toggleRect.pivot = new Vector2(0f, 1f);
            toggleRect.anchoredPosition = anchoredPosition;
            toggleRect.sizeDelta = sizeDelta;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(toggleObject.transform, false);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(18f, 18f);
            var backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.20f, 0.25f, 0.34f, 1f);

            var checkmarkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmarkObject.transform.SetParent(backgroundObject.transform, false);
            var checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;
            var checkmarkImage = checkmarkObject.GetComponent<Image>();
            checkmarkImage.color = new Color(0.24f, 0.68f, 0.87f, 1f);

            var label = CreateLabel("Label", toggleRect, new Vector2(22f, 0f), new Vector2(98f, 24f), TextAnchor.MiddleLeft, 12);
            label.text = stateLabelText;

            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;

            return new ToggleParts(toggle);
        }

        private static HoverHotspotParts CreateHoverHotspot(
            string name,
            RectTransform parent,
            string labelText,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var hotspotObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SettingsHoverRelay));
            hotspotObject.transform.SetParent(parent, false);

            var hotspotRect = hotspotObject.GetComponent<RectTransform>();
            hotspotRect.anchorMin = new Vector2(0f, 1f);
            hotspotRect.anchorMax = new Vector2(0f, 1f);
            hotspotRect.pivot = new Vector2(0f, 1f);
            hotspotRect.anchoredPosition = anchoredPosition;
            hotspotRect.sizeDelta = sizeDelta;

            var hotspotImage = hotspotObject.GetComponent<Image>();
            hotspotImage.color = new Color(0.20f, 0.25f, 0.34f, 1f);
            hotspotImage.raycastTarget = true;

            var label = CreateLabel("Label", hotspotRect, Vector2.zero, sizeDelta, TextAnchor.MiddleCenter, 14);
            label.text = labelText;
            label.raycastTarget = false;

            return new HoverHotspotParts(hotspotRect, hotspotObject.GetComponent<SettingsHoverRelay>());
        }

        private static HoverHintParts CreateHoverHintPanel(
            string name,
            RectTransform parent,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            string labelText)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelObject.transform.SetParent(parent, false);
            panelObject.SetActive(false);

            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = sizeDelta;

            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.11f, 0.15f, 0.96f);
            panelImage.raycastTarget = false;

            var canvasGroup = panelObject.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 1f;

            var label = CreateLabel("ResolutionHoverHintText", panelRect, new Vector2(8f, -8f), new Vector2(232f, 32f), TextAnchor.MiddleLeft, 12);
            label.text = labelText;
            label.raycastTarget = false;

            return new HoverHintParts(panelRect, label);
        }

        private static CountdownStripParts CreateCountdownStrip(
            string name,
            RectTransform parent,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var rootObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            rootObject.transform.SetParent(parent, false);
            rootObject.SetActive(false);

            var rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = sizeDelta;

            var background = rootObject.GetComponent<Image>();
            background.color = new Color(0.10f, 0.13f, 0.18f, 1f);
            background.raycastTarget = false;

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(rootObject.transform, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(sizeDelta.x, 0f);

            var fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.24f, 0.68f, 0.87f, 1f);
            fillImage.type = Image.Type.Simple;
            fillImage.fillAmount = 1f;
            fillImage.raycastTarget = false;

            var label = CreateLabel("DisplayPreviewCountdownText", rootRect, new Vector2(0f, 2f), new Vector2(388f, 18f), TextAnchor.MiddleCenter, 11);
            label.text = string.Empty;
            label.raycastTarget = false;

            return new CountdownStripParts(rootRect, label, fillImage);
        }

        private static object CreateAudioRowRefs(AudioRowParts rowParts)
        {
            var rowRefsType = typeof(SettingsAudioView).GetNestedType("AudioControlRowRefs", BindingFlags.NonPublic);
            if (rowRefsType == null)
            {
                throw new InvalidOperationException("SettingsAudioView is missing authored audio row metadata type.");
            }

            var rowRefs = Activator.CreateInstance(rowRefsType, nonPublic: true);
            if (rowRefs == null)
            {
                throw new InvalidOperationException("Unable to instantiate SettingsAudioView authored audio row metadata.");
            }

            SetField(rowRefs, "_rowRoot", rowParts.RowRoot);
            SetField(rowRefs, "_label", rowParts.Label);
            SetField(rowRefs, "_value", rowParts.Value);
            SetField(rowRefs, "_slider", rowParts.Slider);
            SetField(rowRefs, "_toggle", rowParts.Toggle);
            SetField(rowRefs, "_interactionRelay", rowParts.InteractionRelay);
            return rowRefs;
        }

        private static void SavePrefab(GameObject root, string assetPath)
        {
            try
            {
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException($"Failed to save prefab at '{assetPath}'.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ImportPrefabAssets()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GameplayScreenPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(HelpScreenPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(ObjectiveStatusScreenPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(InventoryScreenPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(SettingsScreenPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(StageResultScreenPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var segments = folderPath.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        private static Font GetDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                   Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private readonly struct ButtonParts
        {
            public ButtonParts(Button button, Text label)
            {
                Button = button;
                Label = label;
            }

            public Button Button { get; }

            public Text Label { get; }
        }

        private readonly struct AudioRowParts
        {
            public AudioRowParts(
                RectTransform rowRoot,
                Text label,
                Text value,
                Slider slider,
                Toggle toggle,
                SettingsSliderInteractionRelay interactionRelay)
            {
                RowRoot = rowRoot;
                Label = label;
                Value = value;
                Slider = slider;
                Toggle = toggle;
                InteractionRelay = interactionRelay;
            }

            public RectTransform RowRoot { get; }

            public Text Label { get; }

            public Text Value { get; }

            public Slider Slider { get; }

            public Toggle Toggle { get; }

            public SettingsSliderInteractionRelay InteractionRelay { get; }
        }

        private readonly struct SliderParts
        {
            public SliderParts(Slider slider, SettingsSliderInteractionRelay relay)
            {
                Slider = slider;
                Relay = relay;
            }

            public Slider Slider { get; }

            public SettingsSliderInteractionRelay Relay { get; }
        }

        private readonly struct ToggleParts
        {
            public ToggleParts(Toggle toggle)
            {
                Toggle = toggle;
            }

            public Toggle Toggle { get; }
        }

        private readonly struct HoverHotspotParts
        {
            public HoverHotspotParts(RectTransform root, SettingsHoverRelay relay)
            {
                Root = root;
                Relay = relay;
            }

            public RectTransform Root { get; }

            public SettingsHoverRelay Relay { get; }
        }

        private readonly struct HoverHintParts
        {
            public HoverHintParts(RectTransform panel, Text label)
            {
                Panel = panel;
                Label = label;
            }

            public RectTransform Panel { get; }

            public Text Label { get; }
        }

        private readonly struct CountdownStripParts
        {
            public CountdownStripParts(RectTransform root, Text label, Image fill)
            {
                Root = root;
                Label = label;
                Fill = fill;
            }

            public RectTransform Root { get; }

            public Text Label { get; }

            public Image Fill { get; }
        }
    }
}
