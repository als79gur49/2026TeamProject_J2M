#if UNITY_EDITOR
using Game.Feature.Stages;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    public static class SceneTransitionOverlayAssetAuthoring
    {
        private const string TransitionRoot = "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions";
        private const string ContentsRoot = TransitionRoot + "/Contents";
        private const string ShellPrefabPath = TransitionRoot + "/SceneTransitionOverlayShell.prefab";
        private const string CatalogPath = TransitionRoot + "/SceneTransitionOverlayContentCatalog.asset";
        private const string GenericContentPrefabPath = ContentsRoot + "/GenericLoadingOverlayContent.prefab";
        private const string ChanceLostContentPrefabPath = ContentsRoot + "/ChanceLostOverlayContent.prefab";

        [MenuItem("Game/UI/Rebuild Scene Transition Overlay Assets")]
        public static void CreateTransitionOverlayAssets()
        {
            var generic = LoadCanonicalContentPrefab<GenericLoadingOverlayContentView>(GenericContentPrefabPath);
            var chanceLost = LoadCanonicalContentPrefab<ChanceLostOverlayContentView>(ChanceLostContentPrefabPath);

            CreateShellPrefab();
            CreateCatalog(generic, chanceLost);
        }

        private static void CreateShellPrefab()
        {
            var root = new GameObject(
                "SceneTransitionOverlayShell",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            var shell = root.AddComponent<SceneTransitionOverlayShellView>();
            try
            {
                UiCanvasElementFactory.Stretch(root.GetComponent<RectTransform>());
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;

                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;

                var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
                blocker.transform.SetParent(root.transform, false);
                UiCanvasElementFactory.Stretch(blocker.GetComponent<RectTransform>());
                var blockerImage = blocker.GetComponent<Image>();
                blockerImage.color = Color.clear;
                blockerImage.raycastTarget = true;

                var visualRoot = new GameObject("VisualRoot", typeof(RectTransform), typeof(CanvasGroup));
                visualRoot.transform.SetParent(root.transform, false);
                UiCanvasElementFactory.Stretch(visualRoot.GetComponent<RectTransform>());

                var contentMount = new GameObject("ContentMount", typeof(RectTransform));
                contentMount.transform.SetParent(visualRoot.transform, false);
                UiCanvasElementFactory.Stretch(contentMount.GetComponent<RectTransform>());

                var serialized = new SerializedObject(shell);
                serialized.FindProperty("_canvas").objectReferenceValue = canvas;
                serialized.FindProperty("_rootGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
                serialized.FindProperty("_blocker").objectReferenceValue = blocker;
                serialized.FindProperty("_blockerImage").objectReferenceValue = blockerImage;
                serialized.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
                serialized.FindProperty("_visualGroup").objectReferenceValue = visualRoot.GetComponent<CanvasGroup>();
                serialized.FindProperty("_contentMount").objectReferenceValue = contentMount.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ShellPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static T LoadCanonicalContentPrefab<T>(string assetPath)
            where T : SceneTransitionOverlayContentView
        {
            var prefab = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException(
                    $"Canonical transition content prefab is missing or has the wrong component type: {assetPath}. " +
                    "Restore or author the canonical prefab explicitly before repairing the transition shell/catalog.");
            }

            return prefab;
        }

        private static void CreateCatalog(
            SceneTransitionOverlayContentView generic,
            SceneTransitionOverlayContentView chanceLost)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SceneTransitionOverlayContentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SceneTransitionOverlayContentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            entries.arraySize = 2;
            SetEntry(
                entries.GetArrayElementAtIndex(0),
                StageTransitionKind.StageClearNext,
                generic);
            SetEntry(
                entries.GetArrayElementAtIndex(1),
                StageTransitionKind.DeathRetryChanceLost,
                chanceLost);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static void SetEntry(
            SerializedProperty entry,
            StageTransitionKind transitionKind,
            SceneTransitionOverlayContentView prefab)
        {
            entry.FindPropertyRelative("_transitionKind").enumValueIndex = (int)transitionKind;
            entry.FindPropertyRelative("_contentPrefab").objectReferenceValue = prefab;
        }
    }
}
#endif
