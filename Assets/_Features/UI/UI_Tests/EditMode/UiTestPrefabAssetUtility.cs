using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    internal static class UiTestPrefabAssetUtility
    {
        internal const string HudPrefabPath = "Assets/_Features/UI/UI_HUD/Prefabs/GameplayHudRoot.prefab";

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
    }
}
