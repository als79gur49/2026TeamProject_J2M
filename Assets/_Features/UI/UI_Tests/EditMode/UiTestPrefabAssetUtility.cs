using Game.Feature.Flow.Audio;
using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Shared.Audio;
using Game.Shared.Display;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    internal static class UiTestPrefabAssetUtility
    {
        internal const string HudPrefabPath = "Assets/_Features/UI/UI_HUD/Prefabs/GameplayHudRoot.prefab";
        internal const string ScreenCatalogPath = "Assets/_Features/UI/UI_Screens/Prefabs/GameplayScreenPrefabCatalog.asset";
        internal const string GameplayScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/GameplayScreen.prefab";
        internal const string HelpScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/HelpScreen.prefab";
        internal const string ObjectiveStatusScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/ObjectiveStatusScreen.prefab";
        internal const string InventoryScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/InventoryScreen.prefab";
        internal const string SettingsScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab";
        internal const string StageResultScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/StageResultScreen.prefab";
        internal const string PopupCatalogPath = "Assets/_Features/UI/UI_Popups/Prefabs/GameplayPopupPrefabCatalog.asset";
        internal const string UiAudioCueMapAssetPath = "Assets/_Features/UI/UI_Composition/Authoring/UiAudioCueMap_V1.asset";
        internal const string PausePopupPrefabPath = "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab";
        internal const string ObjectiveInfoPopupPrefabPath = "Assets/_Features/UI/UI_Popups/Prefabs/ObjectiveInfoPopup.prefab";
        internal const string ConfirmPopupPrefabPath = "Assets/_Features/UI/UI_Popups/Prefabs/ConfirmPopup.prefab";
        internal const string TooltipPopupPrefabPath = "Assets/_Features/UI/UI_Popups/Prefabs/TooltipPopup.prefab";
        internal const string RewardPopupPrefabPath = "Assets/_Features/UI/UI_Popups/Prefabs/RewardPopup.prefab";

        internal static HUDRootView LoadHudPrefab()
        {
            var hudPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(hudPrefabRoot, Is.Not.Null, HudPrefabPath);

            var hudPrefab = hudPrefabRoot.GetComponent<HUDRootView>();
            Assert.That(hudPrefab, Is.Not.Null, HudPrefabPath);
            return hudPrefab;
        }

        internal static HUDRootView InstantiateHudPrefab(RectTransform parent)
        {
            return Object.Instantiate(LoadHudPrefab(), parent, false);
        }

        internal static void AssignHudPrefab(GameplayUiFlowInstaller installer)
        {
            Assert.That(installer, Is.Not.Null);

            var serializedInstaller = new SerializedObject(installer);
            var hudPrefabProperty = serializedInstaller.FindProperty("_hudPrefab");
            Assert.That(hudPrefabProperty, Is.Not.Null);
            hudPrefabProperty.objectReferenceValue = LoadHudPrefab();
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static ScreenPrefabCatalog LoadScreenCatalog()
        {
            var screenCatalog = AssetDatabase.LoadAssetAtPath<ScreenPrefabCatalog>(ScreenCatalogPath);
            Assert.That(screenCatalog, Is.Not.Null, ScreenCatalogPath);
            return screenCatalog;
        }

        internal static TScreenView LoadScreenPrefab<TScreenView>(string assetPath)
            where TScreenView : Component
        {
            var screenPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(screenPrefabRoot, Is.Not.Null, assetPath);

            var screenPrefab = screenPrefabRoot.GetComponent<TScreenView>();
            Assert.That(screenPrefab, Is.Not.Null, assetPath);
            return screenPrefab;
        }

        internal static void AssignScreenPrefabCatalog(GameplayUiFlowInstaller installer)
        {
            Assert.That(installer, Is.Not.Null);

            var serializedInstaller = new SerializedObject(installer);
            var screenCatalogProperty = serializedInstaller.FindProperty("_screenPrefabCatalog");
            Assert.That(screenCatalogProperty, Is.Not.Null);
            screenCatalogProperty.objectReferenceValue = LoadScreenCatalog();
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static PopupPrefabCatalog LoadPopupCatalog()
        {
            var popupCatalog = AssetDatabase.LoadAssetAtPath<PopupPrefabCatalog>(PopupCatalogPath);
            Assert.That(popupCatalog, Is.Not.Null, PopupCatalogPath);
            return popupCatalog;
        }

        internal static TPopupView LoadPopupPrefab<TPopupView>(string assetPath)
            where TPopupView : Component
        {
            var popupPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(popupPrefabRoot, Is.Not.Null, assetPath);

            var popupPrefab = popupPrefabRoot.GetComponent<TPopupView>();
            Assert.That(popupPrefab, Is.Not.Null, assetPath);
            return popupPrefab;
        }

        internal static void AssignPopupPrefabCatalog(GameplayUiFlowInstaller installer)
        {
            Assert.That(installer, Is.Not.Null);

            var serializedInstaller = new SerializedObject(installer);
            var popupCatalogProperty = serializedInstaller.FindProperty("_popupPrefabCatalog");
            Assert.That(popupCatalogProperty, Is.Not.Null);
            popupCatalogProperty.objectReferenceValue = LoadPopupCatalog();
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static UiAudioCueMap LoadUiAudioCueMap()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<UiAudioCueMap>(UiAudioCueMapAssetPath);
            Assert.That(cueMap, Is.Not.Null, UiAudioCueMapAssetPath);
            return cueMap;
        }

        internal static void AssignUiAudioCueMap(GameplayUiFlowInstaller installer)
        {
            Assert.That(installer, Is.Not.Null);

            var serializedInstaller = new SerializedObject(installer);
            var cueMapProperty = serializedInstaller.FindProperty("_uiAudioCueMap");
            Assert.That(cueMapProperty, Is.Not.Null);
            cueMapProperty.objectReferenceValue = LoadUiAudioCueMap();
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void AssignCanonicalUiPrefabs(GameplayUiFlowInstaller installer)
        {
            if (installer.GetComponent<AudioRuntimeInstaller>() == null)
            {
                installer.gameObject.AddComponent<AudioRuntimeInstaller>();
            }

            if (installer.GetComponent<DisplayRuntimeInstaller>() == null)
            {
                installer.gameObject.AddComponent<DisplayRuntimeInstaller>();
            }

            AssignHudPrefab(installer);
            AssignScreenPrefabCatalog(installer);
            AssignPopupPrefabCatalog(installer);
            AssignUiAudioCueMap(installer);
        }

        internal static void ConfigureAudioInstallerBindingMode(
            AudioRuntimeInstaller installer,
            AudioRuntimeInstallerBindingMode bindingMode)
        {
            Assert.That(installer, Is.Not.Null);

            var serializedInstaller = new SerializedObject(installer);
            var bindingModeProperty = serializedInstaller.FindProperty("bindingMode");
            Assert.That(bindingModeProperty, Is.Not.Null);
            bindingModeProperty.enumValueIndex = (int)bindingMode;
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static GlobalAudioFlowBootstrap AssignPersistentAudioFlowBootstrap(GameObject bootstrapRoot)
        {
            Assert.That(bootstrapRoot, Is.Not.Null);

            var audioInstaller = bootstrapRoot.GetComponent<AudioRuntimeInstaller>() ??
                                 bootstrapRoot.AddComponent<AudioRuntimeInstaller>();
            ConfigureAudioInstallerBindingMode(
                audioInstaller,
                AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime);

            var bootstrap = bootstrapRoot.GetComponent<GlobalAudioFlowBootstrap>() ??
                            bootstrapRoot.AddComponent<GlobalAudioFlowBootstrap>();
            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("audioRuntimeInstaller").objectReferenceValue = audioInstaller;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
            return bootstrap;
        }
    }
}
