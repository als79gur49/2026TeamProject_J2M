using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGeneratedBindingPreviewResolver
    {
        public static StageAuthoringGeneratedBindingPreviewModel Resolve(
            StageAuthoringDefinition source,
            StagePlacedEntityAuthoring placement,
            StagePresentationDefinition generatedPresentationDefinition,
            StageDefinition generatedGameplayDefinition = null,
            IReadOnlyList<StageValidationIssue> validationIssues = null)
        {
            if (source == null || placement == null || !StageAuthoringKindRegistry.RequiresPresentationBinding(placement.Kind))
            {
                return StageAuthoringGeneratedBindingPreviewModel.Empty;
            }

            var stableGuid = StageAuthoringProjection.Normalize(placement.StableGuid);
            var allocation = StageAuthoringProjection.BuildAllocationPlan(source);
            var hasEntityId = allocation.EntityIdsByStableGuid.TryGetValue(stableGuid, out var entityId);
            var isPreviewEntityId = IsPreviewEntityId(stableGuid, allocation.NewMappings);
            var lane = StageAuthoringKindRegistry.GetPresentationLane(placement.Kind);
            var presentationId = NormalizePresentationId(lane, placement.PresentationId);
            var relatedIssues = StageAuthoringSelectedIssueFilter.Filter(validationIssues, placement, entityId);
            var actualBinding = FindActualBinding(lane, generatedPresentationDefinition, entityId, out var actualPresentationId);
            var wrongKindBinding = FindWrongKindBinding(lane, generatedPresentationDefinition, entityId);
            var wrongKindGameplay = HasWrongKindSpawn(placement.Kind, generatedGameplayDefinition, entityId);
            var wrongKindIssue = HasIssueCode(relatedIssues, "PresentationBinding.BindingReferencesWrongKind");
            var wrongKind = wrongKindBinding || wrongKindGameplay || wrongKindIssue;
            var expectedBindingExists = hasEntityId && !string.IsNullOrEmpty(presentationId);
            var isSynced = expectedBindingExists && actualBinding && string.Equals(actualPresentationId, presentationId, StringComparison.Ordinal);
            var isMissing = expectedBindingExists && !actualBinding && !wrongKind;
            var isDrifted = expectedBindingExists && actualBinding && !isSynced;
            var statusType = ResolveStatusType(isSynced, isMissing, isDrifted, wrongKind, expectedBindingExists, hasEntityId);
            var status = ResolveStatusLabel(isSynced, isMissing, isDrifted, wrongKind, expectedBindingExists, hasEntityId, isPreviewEntityId);

            return new StageAuthoringGeneratedBindingPreviewModel(
                requiresBinding: true,
                placement.Kind,
                ToBindingKindLabel(lane),
                stableGuid,
                hasEntityId,
                isPreviewEntityId,
                entityId,
                presentationId,
                expectedBindingExists,
                actualBinding,
                isSynced,
                isMissing,
                isDrifted,
                wrongKind,
                status,
                statusType,
                FormatIssues(relatedIssues),
                placement.Cell,
                placement.Facing);
        }

        private static bool IsPreviewEntityId(
            string stableGuid,
            IReadOnlyList<StageAuthoringIdMapping> newMappings)
        {
            if (string.IsNullOrEmpty(stableGuid) || newMappings == null)
            {
                return false;
            }

            for (var i = 0; i < newMappings.Count; i++)
            {
                if (StageAuthoringProjection.Normalize(newMappings[i].StableGuid) == stableGuid)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool FindActualBinding(
            StageAuthoringPresentationLane lane,
            StagePresentationDefinition presentation,
            int entityId,
            out string presentationId)
        {
            presentationId = string.Empty;
            if (presentation == null || entityId <= 0)
            {
                return false;
            }

            if (lane == StageAuthoringPresentationLane.Enemy)
            {
                var bindings = presentation.EnemyPresentationBindings;
                for (var i = 0; i < bindings.Length; i++)
                {
                    if (bindings[i].EntityId == entityId)
                    {
                        presentationId = EnemyPresentationCatalogResolver.NormalizePresentationId(bindings[i].PresentationId);
                        return true;
                    }
                }
            }
            else if (lane == StageAuthoringPresentationLane.Static)
            {
                var bindings = presentation.StaticEntityPresentationBindings;
                for (var i = 0; i < bindings.Length; i++)
                {
                    if (bindings[i].EntityId == entityId)
                    {
                        presentationId = StaticEntityPresentationCatalogResolver.NormalizePresentationId(bindings[i].PresentationId);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool FindWrongKindBinding(
            StageAuthoringPresentationLane lane,
            StagePresentationDefinition presentation,
            int entityId)
        {
            if (presentation == null || entityId <= 0)
            {
                return false;
            }

            if (lane == StageAuthoringPresentationLane.Enemy)
            {
                var staticBindings = presentation.StaticEntityPresentationBindings;
                for (var i = 0; i < staticBindings.Length; i++)
                {
                    if (staticBindings[i].EntityId == entityId)
                    {
                        return true;
                    }
                }
            }
            else if (lane == StageAuthoringPresentationLane.Static)
            {
                var enemyBindings = presentation.EnemyPresentationBindings;
                for (var i = 0; i < enemyBindings.Length; i++)
                {
                    if (enemyBindings[i].EntityId == entityId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasWrongKindSpawn(
            StageAuthoringEntityKind kind,
            StageDefinition gameplay,
            int entityId)
        {
            if (gameplay == null || entityId <= 0)
            {
                return false;
            }

            var spawns = gameplay.Spawns;
            for (var i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].EntityId != entityId)
                {
                    continue;
                }

                return !MatchesKind(kind, spawns[i].Kind);
            }

            return false;
        }

        private static bool MatchesKind(StageAuthoringEntityKind authoringKind, StageSpawnKind spawnKind)
        {
            return authoringKind switch
            {
                StageAuthoringEntityKind.Enemy => spawnKind == StageSpawnKind.Enemy,
                StageAuthoringEntityKind.Box => spawnKind == StageSpawnKind.Box,
                StageAuthoringEntityKind.Wall => spawnKind == StageSpawnKind.Wall,
                StageAuthoringEntityKind.Player => spawnKind == StageSpawnKind.Player,
                _ => false,
            };
        }

        private static string ToBindingKindLabel(StageAuthoringPresentationLane lane)
        {
            return lane == StageAuthoringPresentationLane.Enemy ? "Enemy" : "Static";
        }

        private static MessageType ResolveStatusType(
            bool isSynced,
            bool isMissing,
            bool isDrifted,
            bool wrongKind,
            bool expectedBindingExists,
            bool hasEntityId)
        {
            if (wrongKind || isDrifted)
            {
                return MessageType.Error;
            }

            if (!hasEntityId || !expectedBindingExists || isMissing)
            {
                return MessageType.Warning;
            }

            return isSynced ? MessageType.Info : MessageType.Warning;
        }

        private static string ResolveStatusLabel(
            bool isSynced,
            bool isMissing,
            bool isDrifted,
            bool wrongKind,
            bool expectedBindingExists,
            bool hasEntityId,
            bool isPreviewEntityId)
        {
            if (!hasEntityId)
            {
                return "EntityId mapping is unavailable.";
            }

            if (!expectedBindingExists)
            {
                return "No binding will be generated until PresentationId is set.";
            }

            if (wrongKind)
            {
                return "Wrong Kind.";
            }

            if (isDrifted)
            {
                return "Drifted.";
            }

            if (isMissing)
            {
                return isPreviewEntityId ? "Preview EntityId; binding is not persisted yet." : "Missing.";
            }

            return isSynced ? "Synced." : "Unknown.";
        }

        private static bool HasIssueCode(IReadOnlyList<StageValidationIssue> issues, string code)
        {
            if (issues == null)
            {
                return false;
            }

            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i].Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] FormatIssues(IReadOnlyList<StageValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return Array.Empty<string>();
            }

            var messages = new string[issues.Count];
            for (var i = 0; i < issues.Count; i++)
            {
                messages[i] = $"[{issues[i].Code}] {issues[i].Message}";
            }

            return messages;
        }

        private static string NormalizePresentationId(StageAuthoringPresentationLane lane, string presentationId)
        {
            return lane == StageAuthoringPresentationLane.Enemy
                ? EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId)
                : StaticEntityPresentationCatalogResolver.NormalizePresentationId(presentationId);
        }
    }
}
