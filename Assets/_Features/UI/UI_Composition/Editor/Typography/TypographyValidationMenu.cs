using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Composition.Editor
{
    public static class TypographyValidationMenu
    {
        [MenuItem("Game/UI/Typography/Validate Theme")]
        public static void ValidateTheme()
        {
            TypographyThemeValidator.ValidateProductionTheme().LogToConsole();
        }

        [MenuItem("Game/UI/Typography/Validate Required Prefabs")]
        public static void ValidateRequiredPrefabs()
        {
            TypographyBindingValidator.ValidateRequiredPrefabs().LogToConsole();
        }

        [MenuItem("Game/UI/Typography/Preview Selected/en-US")]
        public static void PreviewSelectedEnglish()
        {
            LogPreviewResult("en-US", TypographyPreviewUtility.ApplyPreviewToSelection("en-US"));
        }

        [MenuItem("Game/UI/Typography/Preview Selected/ko-KR")]
        public static void PreviewSelectedKorean()
        {
            LogPreviewResult("ko-KR", TypographyPreviewUtility.ApplyPreviewToSelection("ko-KR"));
        }

        [MenuItem("Game/UI/Typography/Clear Selected Preview")]
        public static void ClearSelectedPreview()
        {
            var restored = TypographyPreviewUtility.RestorePreviewOnSelection();
            Debug.Log($"Typography preview restore completed: {restored} TMP_Text object(s) restored.");
        }

        [MenuItem("Game/UI/Typography/Bake Selected Preview (Design Stub)")]
        public static void BakeSelectedPreviewDesignStub()
        {
            Debug.Log(
                "Typography preview bake is intentionally not implemented in this slice. " +
                "Future bake design should snapshot authored TMP state, write explicit prefab overrides, " +
                "and require separate asset validation before save.");
        }

        private static void LogPreviewResult(string localeCode, TypographyPreviewResult result)
        {
            if (result.HasErrors)
            {
                Debug.LogError(
                    $"Typography preview '{localeCode}' applied {result.AppliedCount} binding(s), " +
                    $"skipped {result.LocaleInvariantSkippedCount} locale-invariant binding(s), with errors: " +
                    string.Join("; ", result.Errors.Where(error => !string.IsNullOrWhiteSpace(error))));
                return;
            }

            Debug.Log(
                $"Typography preview '{localeCode}' applied {result.AppliedCount} binding(s) and skipped " +
                $"{result.LocaleInvariantSkippedCount} locale-invariant binding(s).");
        }
    }
}
