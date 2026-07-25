using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.UI.Screens;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Composition.Editor
{
    public sealed class TypographyPreviewResult
    {
        private readonly List<string> errors = new();

        public int AppliedCount { get; private set; }

        public int LocaleInvariantSkippedCount { get; private set; }

        public IReadOnlyList<string> Errors => errors;

        public bool HasErrors => errors.Count > 0;

        public void AddApplied()
        {
            AppliedCount++;
        }

        public void AddLocaleInvariantSkipped()
        {
            LocaleInvariantSkippedCount++;
        }

        public void AddError(string message)
        {
            errors.Add(message ?? string.Empty);
        }
    }

    public static class TypographyPreviewUtility
    {
        private static readonly Dictionary<int, TextPreviewSnapshot> PreviewSnapshots = new();

        public static TypographyPreviewResult ApplyPreviewToSelection(
            string localeCode,
            GameplayUiTypographyTheme theme = null)
        {
            var result = new TypographyPreviewResult();
            theme ??= TypographyThemeValidator.FindThemeAsset();
            var selectedGameObjects = Selection.objects.OfType<GameObject>().ToArray();

            var prefabAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var selectedGameObject in selectedGameObjects)
            {
                if (!TryGetPrefabAssetPath(selectedGameObject, out var assetPath) ||
                    !prefabAssetPaths.Add(assetPath))
                {
                    continue;
                }

                Merge(result, ApplyPreviewToPrefabAsset(assetPath, localeCode, theme));
            }

            foreach (var sceneRoot in GetNormalizedSceneRoots(selectedGameObjects))
            {
                Merge(result, ApplyPreview(sceneRoot, localeCode, theme));
            }

            return result;
        }

        public static TypographyPreviewResult ApplyPreviewToPrefabAsset(
            string prefabPath,
            string localeCode,
            GameplayUiTypographyTheme theme = null)
        {
            var result = new TypographyPreviewResult();
            theme ??= TypographyThemeValidator.FindThemeAsset();
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Merge(result, ApplyPreview(prefabRoot, localeCode, theme, recordUndo: false, keepRestoreSnapshot: false));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return result;
        }

        public static TypographyPreviewResult ApplyPreview(
            GameObject root,
            string localeCode,
            GameplayUiTypographyTheme theme,
            bool recordUndo = true,
            bool keepRestoreSnapshot = true)
        {
            var result = new TypographyPreviewResult();
            if (root == null)
            {
                result.AddError("Preview root is null.");
                return result;
            }

            var bindings = root.GetComponentsInChildren<TypographyBinding>(true);
            var requiresTheme = bindings.Length == 0;
            foreach (var binding in bindings)
            {
                var isLocaleInvariant =
                    binding.LocaleParticipation == TypographyLocaleParticipation.LocaleInvariant;
                if (!isLocaleInvariant)
                {
                    requiresTheme = true;
                }

                var target = binding.Target;
                if (target == null)
                {
                    result.AddError($"{binding.name}: TypographyBinding has no TMP_Text target.");
                    continue;
                }

                if (isLocaleInvariant)
                {
                    result.AddLocaleInvariantSkipped();
                    continue;
                }

                if (theme == null)
                {
                    continue;
                }

                if (!theme.TryResolve(localeCode, binding.StyleTag, out var style))
                {
                    result.AddError($"{target.name}: StyleTag '{binding.StyleTag}' does not resolve for '{localeCode}'.");
                    continue;
                }

                if (keepRestoreSnapshot && !PreviewSnapshots.ContainsKey(target.GetInstanceID()))
                {
                    PreviewSnapshots.Add(target.GetInstanceID(), TextPreviewSnapshot.Capture(target));
                }

                if (recordUndo)
                {
                    Undo.RecordObject(target, "Apply Typography Preview");
                }

                LocalizedTmpTextApplicator.ApplyResolvedTypography(target, style, binding);
                result.AddApplied();
            }

            if (theme == null && requiresTheme)
            {
                result.AddError("GameplayUiTypographyTheme asset was not found.");
            }

            return result;
        }

        public static int RestorePreview(GameObject root, bool recordUndo = true)
        {
            if (root == null)
            {
                return 0;
            }

            var restored = 0;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!PreviewSnapshots.TryGetValue(text.GetInstanceID(), out var snapshot))
                {
                    continue;
                }

                if (recordUndo)
                {
                    Undo.RecordObject(text, "Clear Typography Preview");
                }

                snapshot.Restore(text);
                PreviewSnapshots.Remove(text.GetInstanceID());
                restored++;
            }

            return restored;
        }

        public static int RestorePreviewOnSelection()
        {
            var restored = 0;
            foreach (var selectedObject in GetNormalizedSceneRoots(Selection.gameObjects))
            {
                restored += RestorePreview(selectedObject);
            }

            return restored;
        }

        private static IReadOnlyList<GameObject> GetNormalizedSceneRoots(
            IReadOnlyList<GameObject> selectedGameObjects)
        {
            var sceneObjects = selectedGameObjects
                .Where(selectedObject => selectedObject != null &&
                                         !TryGetPrefabAssetPath(selectedObject, out _))
                .Distinct()
                .ToArray();
            return sceneObjects
                .Where(candidate => !sceneObjects.Any(other =>
                    other != candidate && candidate.transform.IsChildOf(other.transform)))
                .ToArray();
        }

        private static bool TryGetPrefabAssetPath(GameObject gameObject, out string assetPath)
        {
            assetPath = gameObject != null ? AssetDatabase.GetAssetPath(gameObject) : string.Empty;
            return !string.IsNullOrWhiteSpace(assetPath) &&
                   PrefabUtility.IsPartOfPrefabAsset(gameObject);
        }

        private static void Merge(TypographyPreviewResult target, TypographyPreviewResult source)
        {
            target ??= new TypographyPreviewResult();
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.AppliedCount; i++)
            {
                target.AddApplied();
            }

            for (var i = 0; i < source.LocaleInvariantSkippedCount; i++)
            {
                target.AddLocaleInvariantSkipped();
            }

            foreach (var error in source.Errors)
            {
                target.AddError(error);
            }
        }

        private readonly struct TextPreviewSnapshot
        {
            private readonly TMP_FontAsset font;
            private readonly Material material;
            private readonly FontStyles fontStyle;
            private readonly float fontSize;
            private readonly bool enableAutoSizing;
            private readonly float fontSizeMin;
            private readonly float fontSizeMax;
            private readonly float lineSpacing;
            private readonly float characterSpacing;

            private TextPreviewSnapshot(
                TMP_FontAsset font,
                Material material,
                FontStyles fontStyle,
                float fontSize,
                bool enableAutoSizing,
                float fontSizeMin,
                float fontSizeMax,
                float lineSpacing,
                float characterSpacing)
            {
                this.font = font;
                this.material = material;
                this.fontStyle = fontStyle;
                this.fontSize = fontSize;
                this.enableAutoSizing = enableAutoSizing;
                this.fontSizeMin = fontSizeMin;
                this.fontSizeMax = fontSizeMax;
                this.lineSpacing = lineSpacing;
                this.characterSpacing = characterSpacing;
            }

            public static TextPreviewSnapshot Capture(TMP_Text target)
            {
                return new TextPreviewSnapshot(
                    target.font,
                    target.fontSharedMaterial,
                    target.fontStyle,
                    target.fontSize,
                    target.enableAutoSizing,
                    target.fontSizeMin,
                    target.fontSizeMax,
                    target.lineSpacing,
                    target.characterSpacing);
            }

            public void Restore(TMP_Text target)
            {
                target.font = font;
                target.fontSharedMaterial = material;
                target.fontStyle = fontStyle;
                target.fontSize = fontSize;
                target.enableAutoSizing = enableAutoSizing;
                target.fontSizeMin = fontSizeMin;
                target.fontSizeMax = fontSizeMax;
                target.lineSpacing = lineSpacing;
                target.characterSpacing = characterSpacing;
            }
        }
    }
}
