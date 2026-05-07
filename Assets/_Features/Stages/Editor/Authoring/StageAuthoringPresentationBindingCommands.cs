using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum TileFeatureVisualBindingStatusKind
    {
        NoPresentationDefinition,
        MissingTileFeature,
        MissingBinding,
        Bound,
        DuplicateBinding,
        InvalidPrefab,
    }

    internal readonly struct TileFeatureVisualBindingStatus
    {
        public TileFeatureVisualBindingStatus(
            TileFeatureVisualBindingStatusKind kind,
            int tileId,
            GameObject visualPrefab,
            int bindingCount,
            string message)
        {
            Kind = kind;
            TileId = tileId;
            VisualPrefab = visualPrefab;
            BindingCount = bindingCount;
            Message = message ?? string.Empty;
        }

        public TileFeatureVisualBindingStatusKind Kind { get; }

        public int TileId { get; }

        public GameObject VisualPrefab { get; }

        public int BindingCount { get; }

        public string Message { get; }
    }

    internal static class StageAuthoringPresentationBindingCommands
    {
        public static bool TrySetTileFeatureVisualBinding(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            int tileId,
            GameObject visualPrefab,
            out string error)
        {
            error = string.Empty;
            if (!ValidateBindingTarget(presentation, authoring, tileId, out error))
            {
                return false;
            }

            if (visualPrefab == null)
            {
                error = "TileFeature visual binding requires a visual prefab.";
                return false;
            }

            if (!PrefabHasConfigurableTileFeatureVisualTarget(visualPrefab))
            {
                error = $"Prefab '{visualPrefab.name}' must provide a configurable TileFeature visual target.";
                return false;
            }

            var next = presentation.TileFeaturePresentationBindings
                .Where(binding => binding != null && binding.TileId != tileId)
                .Select(CloneBinding)
                .ToList();
            next.Add(new TileFeaturePresentationBinding
            {
                TileId = tileId,
                VisualPrefab = visualPrefab,
            });
            WriteBindings(presentation, next, "Set TileFeature Visual Binding");
            return true;
        }

        public static bool TryRemoveTileFeatureVisualBinding(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            int tileId,
            out string error)
        {
            error = string.Empty;
            if (!ValidateBindingTarget(presentation, authoring, tileId, out error))
            {
                return false;
            }

            var removedCount = 0;
            var next = new List<TileFeaturePresentationBinding>();
            var bindings = presentation.TileFeaturePresentationBindings;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                if (binding.TileId == tileId)
                {
                    removedCount++;
                    continue;
                }

                next.Add(CloneBinding(binding));
            }

            if (removedCount <= 0)
            {
                error = $"TileFeature visual binding for TileId {tileId} was not found.";
                return false;
            }

            WriteBindings(presentation, next, "Remove TileFeature Visual Binding");
            return true;
        }

        public static bool TryGetTileFeatureVisualBindingStatus(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            int tileId,
            out TileFeatureVisualBindingStatus status)
        {
            if (presentation == null)
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.NoPresentationDefinition,
                    tileId,
                    null,
                    0,
                    "No StagePresentationDefinition assigned; visual binding editing disabled.");
                return false;
            }

            if (authoring == null ||
                tileId <= 0 ||
                !ContainsTileFeature(authoring, tileId))
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.MissingTileFeature,
                    tileId,
                    null,
                    0,
                    $"TileFeature TileId {tileId} is not present in the selected authoring definition.");
                return false;
            }

            var matched = presentation.TileFeaturePresentationBindings
                .Where(binding => binding != null && binding.TileId == tileId)
                .ToArray();
            if (matched.Length == 0)
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.MissingBinding,
                    tileId,
                    null,
                    0,
                    "TileFeature visual binding is missing.");
                return true;
            }

            var prefab = matched[0].VisualPrefab;
            if (matched.Length > 1)
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.DuplicateBinding,
                    tileId,
                    prefab,
                    matched.Length,
                    $"TileFeature visual binding is duplicated ({matched.Length}).");
                return true;
            }

            if (!PrefabHasConfigurableTileFeatureVisualTarget(prefab))
            {
                status = new TileFeatureVisualBindingStatus(
                    TileFeatureVisualBindingStatusKind.InvalidPrefab,
                    tileId,
                    prefab,
                    1,
                    prefab == null
                        ? "TileFeature visual binding has no visual prefab."
                        : $"TileFeature visual binding prefab '{prefab.name}' is invalid.");
                return true;
            }

            status = new TileFeatureVisualBindingStatus(
                TileFeatureVisualBindingStatusKind.Bound,
                tileId,
                prefab,
                1,
                $"Bound: {prefab.name}");
            return true;
        }

        public static bool PrefabHasConfigurableTileFeatureVisualTarget(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            var behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour is TileFeatureVisualTargetView ||
                    behaviour is ITileFeatureVisualTarget &&
                    behaviour is ITileFeatureVisualTargetConfigurator)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ValidateBindingTarget(
            StagePresentationDefinition presentation,
            StageAuthoringDefinition authoring,
            int tileId,
            out string error)
        {
            error = string.Empty;
            if (presentation == null)
            {
                error = "StagePresentationDefinition is missing.";
                return false;
            }

            if (authoring == null)
            {
                error = "StageAuthoringDefinition is missing.";
                return false;
            }

            if (tileId <= 0)
            {
                error = "TileFeature visual binding requires a positive TileId.";
                return false;
            }

            if (!ContainsTileFeature(authoring, tileId))
            {
                error = $"TileFeature TileId {tileId} was not found in the authoring definition.";
                return false;
            }

            return true;
        }

        private static bool ContainsTileFeature(StageAuthoringDefinition authoring, int tileId)
        {
            if (authoring == null)
            {
                return false;
            }

            var features = authoring.TileFeatures;
            for (var i = 0; i < features.Count; i++)
            {
                if (features[i].TileId == tileId)
                {
                    return true;
                }
            }

            return false;
        }

        private static TileFeaturePresentationBinding CloneBinding(TileFeaturePresentationBinding binding)
        {
            return new TileFeaturePresentationBinding
            {
                TileId = binding.TileId,
                VisualPrefab = binding.VisualPrefab,
            };
        }

        private static void WriteBindings(
            StagePresentationDefinition presentation,
            IReadOnlyList<TileFeaturePresentationBinding> bindings,
            string undoName)
        {
            Undo.RecordObject(presentation, undoName);
            var ordered = bindings
                .Where(binding => binding != null)
                .OrderBy(binding => binding.TileId)
                .ToArray();
            var serializedObject = new SerializedObject(presentation);
            var property = serializedObject.FindProperty("tileFeaturePresentationBindings");
            property.arraySize = ordered.Length;
            for (var i = 0; i < ordered.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("TileId").intValue = ordered[i].TileId;
                element.FindPropertyRelative("VisualPrefab").objectReferenceValue = ordered[i].VisualPrefab;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presentation);
        }
    }
}
