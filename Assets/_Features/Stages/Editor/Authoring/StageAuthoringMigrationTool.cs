using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageAuthoringMigrationTool
    {
        public static StageAuthoringDefinition CreateForEntry(StageContentEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.GameplayDefinition == null)
            {
                throw new InvalidOperationException($"StageContentEntry '{entry.name}' requires a gameplay definition to migrate.");
            }

            var entryPath = AssetDatabase.GetAssetPath(entry);
            if (string.IsNullOrEmpty(entryPath))
            {
                throw new InvalidOperationException($"StageContentEntry '{entry.name}' must be saved before migration.");
            }

            var stageId = entry.StageId.IsValid
                ? entry.StageId.Value
                : StageAuthoringGenerator.Normalize(entry.GameplayDefinition.name);
            var folder = System.IO.Path.GetDirectoryName(entryPath)?.Replace('\\', '/');
            var authoringPath = $"{folder}/{stageId}_Authoring.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageAuthoringDefinition>(authoringPath);
            if (existing != null)
            {
                PopulateFromOutputs(existing, entry, overwriteGeneratedReferences: true);
                existing.SetOwnerMetadata(entry, AssetDatabase.AssetPathToGUID(entryPath));
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            authoring.name = $"{stageId}_Authoring";
            AssetDatabase.CreateAsset(authoring, authoringPath);
            PopulateFromOutputs(authoring, entry, overwriteGeneratedReferences: true);
            authoring.SetOwnerMetadata(entry, AssetDatabase.AssetPathToGUID(entryPath));
            EditorUtility.SetDirty(authoring);
            AssetDatabase.SaveAssets();
            return authoring;
        }

        public static void PopulateFromOutputs(
            StageAuthoringDefinition authoring,
            StageContentEntry entry,
            bool overwriteGeneratedReferences)
        {
            if (authoring == null)
            {
                throw new ArgumentNullException(nameof(authoring));
            }

            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            PopulateFromOutputs(
                authoring,
                entry.StageId,
                entry.GameplayDefinition,
                entry.PresentationDefinition,
                overwriteGeneratedReferences);
        }

        public static void PopulateFromOutputs(
            StageAuthoringDefinition authoring,
            StageId stageId,
            StageDefinition gameplay,
            StagePresentationDefinition presentation,
            bool overwriteGeneratedReferences)
        {
            if (authoring == null)
            {
                throw new ArgumentNullException(nameof(authoring));
            }

            if (gameplay == null)
            {
                throw new ArgumentNullException(nameof(gameplay));
            }

            Undo.RecordObject(authoring, "Migrate Stage Authoring Definition");
            if (overwriteGeneratedReferences)
            {
                authoring.AssignGeneratedDefinitions(gameplay, presentation);
            }

            var enemyBindings = BuildEnemyBindingsByEntityId(presentation);
            var staticBindings = BuildStaticBindingsByEntityId(presentation);
            var placements = new List<StagePlacedEntityAuthoring>();
            var mappings = new List<StageAuthoringIdMapping>();
            AddPlacements(stageId, gameplay.PlayerSpawns, StageAuthoringEntityKind.Player, null, placements, mappings);
            AddPlacements(stageId, gameplay.EnemySpawns, StageAuthoringEntityKind.Enemy, enemyBindings, placements, mappings);
            AddPlacements(stageId, gameplay.BoxSpawns, StageAuthoringEntityKind.Box, staticBindings, placements, mappings);
            AddPlacements(stageId, gameplay.WallSpawns, StageAuthoringEntityKind.Wall, staticBindings, placements, mappings);

            authoring.SetBoard(gameplay.Board);
            authoring.SetPlacements(placements);
            authoring.SetTileFeatures(gameplay.TileFeatures);
            authoring.SetZones(gameplay.Zones);
            authoring.SetObjective(gameplay.Objective);
            authoring.SetEntityIdMappings(mappings);
            EditorUtility.SetDirty(authoring);
        }

        private static Dictionary<int, string> BuildEnemyBindingsByEntityId(StagePresentationDefinition presentation)
        {
            var result = new Dictionary<int, string>();
            if (presentation == null)
            {
                return result;
            }

            var bindings = presentation.EnemyPresentationBindings;
            for (var i = 0; i < bindings.Length; i++)
            {
                result[bindings[i].EntityId] = StageAuthoringGenerator.Normalize(bindings[i].PresentationId);
            }

            return result;
        }

        private static Dictionary<int, string> BuildStaticBindingsByEntityId(StagePresentationDefinition presentation)
        {
            var result = new Dictionary<int, string>();
            if (presentation == null)
            {
                return result;
            }

            var bindings = presentation.StaticEntityPresentationBindings;
            for (var i = 0; i < bindings.Length; i++)
            {
                result[bindings[i].EntityId] = StageAuthoringGenerator.Normalize(bindings[i].PresentationId);
            }

            return result;
        }

        private static void AddPlacements(
            StageId stageId,
            IReadOnlyList<StageSpawnDefinition> spawns,
            StageAuthoringEntityKind kind,
            IReadOnlyDictionary<int, string> presentationByEntityId,
            ICollection<StagePlacedEntityAuthoring> placements,
            ICollection<StageAuthoringIdMapping> mappings)
        {
            if (spawns == null)
            {
                return;
            }

            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                var stableGuid = BuildStableGuid(stageId, kind, spawn.EntityId);
                var presentationId = presentationByEntityId != null &&
                                     presentationByEntityId.TryGetValue(spawn.EntityId, out var boundPresentationId)
                    ? boundPresentationId
                    : string.Empty;
                placements.Add(new StagePlacedEntityAuthoring
                {
                    StableGuid = stableGuid,
                    DisplayName = $"{kind} {spawn.EntityId}",
                    Kind = kind,
                    Cell = spawn.Cell,
                    Facing = spawn.Facing,
                    Hp = spawn.Hp,
                    UnitStackGroup = spawn.UnitStackGroup,
                    BoxCapabilities = spawn.BoxCapabilities,
                    BoxArchetype = spawn.BoxArchetype,
                    EnemyAiMode = spawn.EnemyAiMode,
                    EnemyAiStateTimer = spawn.EnemyAiStateTimer,
                    EnemyAiProfileOverride = spawn.EnemyAiProfile,
                    PresentationId = presentationId,
                });
                mappings.Add(new StageAuthoringIdMapping
                {
                    StableGuid = stableGuid,
                    EntityId = spawn.EntityId,
                    Retired = false,
                    LastKnownDisplayName = $"{kind} {spawn.EntityId}",
                });
            }
        }

        private static string BuildStableGuid(StageId stageId, StageAuthoringEntityKind kind, int entityId)
        {
            var stageKey = stageId.IsValid ? stageId.Value : "stage";
            return $"{stageKey}:{kind}:{entityId}";
        }
    }
}
