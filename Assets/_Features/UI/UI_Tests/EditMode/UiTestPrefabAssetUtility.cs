using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    internal static class UiTestPrefabAssetUtility
    {
        internal const string HudPrefabPath = "Assets/_Features/UI/UI_HUD/Prefabs/GameplayHudRoot.prefab";
        internal const string PopupCatalogPath = "Assets/_Features/UI/UI_Popups/Prefabs/GameplayPopupPrefabCatalog.asset";
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

        internal static void AssignCanonicalUiPrefabs(GameplayUiFlowInstaller installer)
        {
            AssignHudPrefab(installer);
            AssignPopupPrefabCatalog(installer);
        }
    }
}
