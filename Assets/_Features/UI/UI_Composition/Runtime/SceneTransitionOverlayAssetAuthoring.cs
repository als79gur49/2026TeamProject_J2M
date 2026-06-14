#if UNITY_EDITOR
using System.IO;
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
        private const string AllIn1UiStencilMaterialPath = "Assets/Plugins/AllIn1SpriteShader/Materials/UIStencil.mat";
        private const string FilledIconName = "FilledIcon";

        [MenuItem("Game/UI/Rebuild Scene Transition Overlay Assets")]
        public static void CreateTransitionOverlayAssets()
        {
            Directory.CreateDirectory(TransitionRoot);
            Directory.CreateDirectory(ContentsRoot);

            var generic = CreateContentPrefab<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");
            var chanceLost = CreateContentPrefab<ChanceLostOverlayContentView>("ChanceLostOverlayContent");

            CreateShellPrefab();
            CreateCatalog(generic, chanceLost);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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

        private static T CreateContentPrefab<T>(string name)
            where T : SceneTransitionOverlayContentView
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var view = root.AddComponent<T>();
            try
            {
                UiCanvasElementFactory.Stretch(root.GetComponent<RectTransform>());

                var panel = UiCanvasElementFactory.CreatePanel(
                    "Panel",
                    root.transform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(700f, 300f),
                    Vector2.zero);
                panel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.13f, 0.96f);

                var progressText = UiCanvasElementFactory.CreateLabel("ProgressText_TMP", panel, new Vector2(40f, -132f), new Vector2(620f, 24f), TextAnchor.MiddleCenter, 14);

                var serialized = new SerializedObject(view);
                serialized.FindProperty("_rootGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
                serialized.FindProperty("_progressText").objectReferenceValue = progressText;

                if (view is ChanceLostOverlayContentView)
                {
                    var chanceSlots = CreateChanceSlotRoots(panel);
                    var chanceSlotRoots = serialized.FindProperty("_chanceSlotRoots");
                    chanceSlotRoots.arraySize = chanceSlots.Length;
                    for (var i = 0; i < chanceSlots.Length; i++)
                    {
                        chanceSlotRoots.GetArrayElementAtIndex(i).objectReferenceValue = chanceSlots[i];
                    }

                    serialized.FindProperty("_allIn1EffectMaterialTemplate").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Material>(AllIn1UiStencilMaterialPath);
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ContentsRoot}/{name}.prefab");
                return prefab.GetComponent<T>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform[] CreateChanceSlotRoots(Transform parent)
        {
            var row = new GameObject("ChanceSlotRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(180f, 44f);
            rowRect.anchoredPosition = new Vector2(0f, -112f);

            var slots = new RectTransform[3];
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = new GameObject($"ChanceSlotView {i}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                slot.transform.SetParent(row.transform, false);
                var rect = slot.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(34f, 34f);
                rect.anchoredPosition = new Vector2((i - 1) * 54f, 0f);

                var image = slot.GetComponent<Image>();
                image.color = new Color(0.25f, 0.29f, 0.36f, 0.75f);
                image.raycastTarget = false;

                var filledIcon = new GameObject(FilledIconName, typeof(RectTransform), typeof(Image));
                filledIcon.transform.SetParent(slot.transform, false);
                var filledIconRect = filledIcon.GetComponent<RectTransform>();
                UiCanvasElementFactory.Stretch(filledIconRect);
                filledIconRect.sizeDelta = new Vector2(-4f, -4f);
                var filledIconImage = filledIcon.GetComponent<Image>();
                filledIconImage.color = new Color(0.95f, 0.22f, 0.18f, 1f);
                filledIconImage.raycastTarget = false;

                var effect = new GameObject("Effect", typeof(RectTransform), typeof(Image));
                effect.transform.SetParent(slot.transform, false);
                UiCanvasElementFactory.Stretch(effect.GetComponent<RectTransform>());
                var effectImage = effect.GetComponent<Image>();
                effectImage.color = new Color(1f, 0.12f, 0.12f, 0.12f);
                effectImage.raycastTarget = false;
                slots[i] = rect;
            }

            return slots;
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
            serialized.FindProperty("_genericFallbackPrefab").objectReferenceValue = generic;
            var entries = serialized.FindProperty("_entries");
            entries.arraySize = 7;
            SetEntry(entries.GetArrayElementAtIndex(0), StageTransitionKind.MainToGameplay, TransitionOverlayKind.GenericLoading, generic);
            SetEntry(entries.GetArrayElementAtIndex(1), StageTransitionKind.GameplayToMain, TransitionOverlayKind.MainMenuReturn, generic);
            SetEntry(entries.GetArrayElementAtIndex(2), StageTransitionKind.StageClearNext, TransitionOverlayKind.StageClear, generic);
            SetEntry(entries.GetArrayElementAtIndex(3), StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart, generic);
            SetEntry(entries.GetArrayElementAtIndex(4), StageTransitionKind.DeathRetryChanceLost, TransitionOverlayKind.ChanceLost, chanceLost);
            SetEntry(entries.GetArrayElementAtIndex(5), StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart, generic);
            SetEntry(entries.GetArrayElementAtIndex(6), StageTransitionKind.Unknown, TransitionOverlayKind.GenericLoading, generic);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void SetEntry(
            SerializedProperty entry,
            StageTransitionKind transitionKind,
            TransitionOverlayKind overlayKind,
            SceneTransitionOverlayContentView prefab)
        {
            entry.FindPropertyRelative("_transitionKind").enumValueIndex = (int)transitionKind;
            entry.FindPropertyRelative("_fallbackOverlayKind").enumValueIndex = (int)overlayKind;
            entry.FindPropertyRelative("_contentPrefab").objectReferenceValue = prefab;
        }
    }
}
#endif
