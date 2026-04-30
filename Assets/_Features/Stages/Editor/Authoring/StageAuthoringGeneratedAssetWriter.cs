using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGeneratedAssetWriter
    {
        public static void ApplyPlan(
            StageAuthoringGenerationPlan plan,
            StageAuthoringGenerateOptions options)
        {
            if (plan == null || plan.Source == null || plan.BuildData == null || plan.Allocation == null)
            {
                return;
            }

            options ??= plan.Options ?? StageAuthoringGenerateOptions.WriteAll;
            ApplyMappings(plan.Source, plan.Allocation.Mappings);
            if (options.WriteGameplay && plan.GameplayOutput != null)
            {
                ApplyGameplayOutput(plan.GameplayOutput, plan.BuildData);
            }

            if (options.WritePresentationBindings && plan.PresentationOutput != null)
            {
                ApplyPresentationOutput(plan.PresentationOutput, plan.BuildData);
            }

            AssetDatabase.SaveAssets();
        }

        public static void ApplyGameplayOutput(
            StageDefinition stage,
            StageAuthoringBuildData buildData,
            bool recordUndo = true,
            bool markDirty = true)
        {
            ApplyGameplayOutput(stage, buildData.GameplayPayload, recordUndo, markDirty);
        }

        public static void ApplyGameplayOutput(
            StageDefinition stage,
            StageAuthoringGameplayWritePayload payload,
            bool recordUndo = true,
            bool markDirty = true)
        {
            if (recordUndo)
            {
                Undo.RecordObject(stage, "Generate Stage Gameplay Definition");
            }

            var serializedObject = new SerializedObject(stage);
            SetBoard(serializedObject.FindProperty("board"), payload.Board);
            SetSpawnArray(serializedObject.FindProperty("playerSpawns"), payload.PlayerSpawns);
            SetSpawnArray(serializedObject.FindProperty("enemySpawns"), payload.EnemySpawns);
            SetSpawnArray(serializedObject.FindProperty("boxSpawns"), payload.BoxSpawns);
            SetSpawnArray(serializedObject.FindProperty("wallSpawns"), payload.WallSpawns);
            SetZoneArray(serializedObject.FindProperty("zones"), payload.Zones);
            SetObjective(serializedObject.FindProperty("objective"), payload.Objective);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (markDirty)
            {
                EditorUtility.SetDirty(stage);
            }
        }

        public static void ApplyPresentationOutput(
            StagePresentationDefinition presentation,
            StageAuthoringBuildData buildData,
            bool recordUndo = true,
            bool markDirty = true)
        {
            ApplyPresentationOutput(presentation, buildData.PresentationBindingPayload, recordUndo, markDirty);
        }

        public static void ApplyPresentationOutput(
            StagePresentationDefinition presentation,
            StageAuthoringPresentationBindingWritePayload payload,
            bool recordUndo = true,
            bool markDirty = true)
        {
            if (recordUndo)
            {
                Undo.RecordObject(presentation, "Generate Stage Presentation Bindings");
            }

            var serializedObject = new SerializedObject(presentation);
            SetEnemyBindingArray(
                serializedObject.FindProperty("enemyPresentationBindings"),
                payload.EnemyPresentationBindings);
            SetStaticBindingArray(
                serializedObject.FindProperty("staticEntityPresentationBindings"),
                payload.StaticEntityPresentationBindings);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (markDirty)
            {
                EditorUtility.SetDirty(presentation);
            }
        }

        private static void ApplyMappings(
            StageAuthoringDefinition source,
            IReadOnlyList<StageAuthoringIdMapping> mappings)
        {
            Undo.RecordObject(source, "Generate Stage Authoring Entity IDs");
            source.SetEntityIdMappings(mappings);
            EditorUtility.SetDirty(source);
        }

        private static void SetBoard(SerializedProperty property, StageBoardDefinition board)
        {
            property.FindPropertyRelative("MinInclusive").vector2IntValue = board.MinInclusive;
            property.FindPropertyRelative("MaxInclusive").vector2IntValue = board.MaxInclusive;
            property.FindPropertyRelative("InitialBottomFace").intValue = (int)board.InitialBottomFace;
        }

        private static void SetSpawnArray(
            SerializedProperty property,
            IReadOnlyList<StageSpawnDefinition> spawns)
        {
            property.arraySize = spawns.Count;
            for (var i = 0; i < spawns.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var spawn = spawns[i];
                element.FindPropertyRelative("EntityId").intValue = spawn.EntityId;
                element.FindPropertyRelative("Kind").intValue = (int)spawn.Kind;
                SetSurfaceCell(element.FindPropertyRelative("Cell"), spawn.Cell);
                element.FindPropertyRelative("Facing").intValue = (int)spawn.Facing;
                element.FindPropertyRelative("Hp").intValue = spawn.Hp;
                element.FindPropertyRelative("BoxCapabilities").intValue = (int)spawn.BoxCapabilities;
                element.FindPropertyRelative("EnemyAiMode").intValue = (int)spawn.EnemyAiMode;
                element.FindPropertyRelative("EnemyAiStateTimer").intValue = spawn.EnemyAiStateTimer;
                element.FindPropertyRelative("EnemyAiProfile").objectReferenceValue = spawn.EnemyAiProfile;
                element.FindPropertyRelative("PresentationId").stringValue = string.Empty;
                element.FindPropertyRelative("UnitStackGroup").stringValue = Normalize(spawn.UnitStackGroup);
            }
        }

        private static void SetSurfaceCell(SerializedProperty property, Game.Feature.Gameplay.BoardState.SurfaceCell cell)
        {
            property.FindPropertyRelative("face").intValue = (int)cell.face;
            property.FindPropertyRelative("x").intValue = cell.x;
            property.FindPropertyRelative("y").intValue = cell.y;
        }

        private static void SetZoneArray(
            SerializedProperty property,
            IReadOnlyList<StageZoneDefinition> zones)
        {
            property.arraySize = zones.Count;
            for (var i = 0; i < zones.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var zone = zones[i];
                element.FindPropertyRelative("ZoneId").stringValue = Normalize(zone.ZoneId);
                element.FindPropertyRelative("FaceId").intValue = (int)zone.FaceId;
                var regions = zone.GetRegionsOrEmpty();
                var regionsProperty = element.FindPropertyRelative("Regions");
                regionsProperty.arraySize = regions.Length;
                for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
                {
                    var regionProperty = regionsProperty.GetArrayElementAtIndex(regionIndex);
                    regionProperty.FindPropertyRelative("MinInclusive").vector2IntValue = regions[regionIndex].MinInclusive;
                    regionProperty.FindPropertyRelative("MaxInclusive").vector2IntValue = regions[regionIndex].MaxInclusive;
                }
            }
        }

        private static void SetObjective(SerializedProperty property, StageObjectiveAuthoring objective)
        {
            property.FindPropertyRelative("CompletionPolicy").intValue = (int)objective.CompletionPolicy;
            var entries = objective.GetConditionEntriesOrEmpty();
            var entriesProperty = property.FindPropertyRelative("ConditionEntries");
            entriesProperty.arraySize = entries.Length;
            for (var i = 0; i < entries.Length; i++)
            {
                var element = entriesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Condition").objectReferenceValue = entries[i].Condition;
                element.FindPropertyRelative("Required").boolValue = entries[i].Required;
                element.FindPropertyRelative("Role").intValue = (int)entries[i].Role;
                element.FindPropertyRelative("StableConditionId").stringValue = Normalize(entries[i].StableConditionId);
            }
        }

        private static void SetEnemyBindingArray(
            SerializedProperty property,
            IReadOnlyList<EnemyPresentationBinding> bindings)
        {
            property.arraySize = bindings.Count;
            for (var i = 0; i < bindings.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("EntityId").intValue = bindings[i].EntityId;
                element.FindPropertyRelative("PresentationId").stringValue = Normalize(bindings[i].PresentationId);
            }
        }

        private static void SetStaticBindingArray(
            SerializedProperty property,
            IReadOnlyList<StaticEntityPresentationBinding> bindings)
        {
            property.arraySize = bindings.Count;
            for (var i = 0; i < bindings.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("EntityId").intValue = bindings[i].EntityId;
                element.FindPropertyRelative("PresentationId").stringValue = Normalize(bindings[i].PresentationId);
            }
        }

        private static string Normalize(string value)
        {
            return StageAuthoringGenerator.Normalize(value);
        }
    }
}
