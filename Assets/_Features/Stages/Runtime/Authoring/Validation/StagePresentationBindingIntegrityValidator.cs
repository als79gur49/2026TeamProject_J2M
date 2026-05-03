using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    internal static class StagePresentationBindingIntegrityValidator
    {
        public static void ValidateGeneratedBindings(
            StageDefinition gameplay,
            StagePresentationDefinition presentation,
            StageAuthoringPresentationCatalogSnapshot enemySnapshot,
            StageAuthoringPresentationCatalogSnapshot staticSnapshot,
            bool strict,
            StageCatalogValidationOptions options,
            string stageId,
            string authoringAssetName,
            ICollection<StageValidationIssue> issues)
        {
            var spawnMap = BuildSpawnMap(gameplay);
            var enemySpawnEntityIds = BuildSpawnEntityIds(gameplay.EnemySpawns);
            var staticSpawnEntityIds = BuildStaticSpawnEntityIds(gameplay);
            var presentationPath = StagePresentationCatalogIssueFactory.GetAssetPath(presentation, options);
            var enemyBindingsByEntityId = new HashSet<int>();
            var staticBindingsByEntityId = new HashSet<int>();

            var enemyBindings = presentation.EnemyPresentationBindings;
            for (var i = 0; i < enemyBindings.Length; i++)
            {
                var binding = enemyBindings[i];
                enemyBindingsByEntityId.Add(binding.EntityId);
                ValidateBindingTarget(
                    binding.EntityId,
                    binding.PresentationId,
                    i,
                    enemyBinding: true,
                    spawnMap,
                    enemySnapshot,
                    strict,
                    options,
                    stageId,
                    authoringAssetName,
                    presentation,
                    presentationPath,
                    issues);
            }

            var staticBindings = presentation.StaticEntityPresentationBindings;
            for (var i = 0; i < staticBindings.Length; i++)
            {
                var binding = staticBindings[i];
                staticBindingsByEntityId.Add(binding.EntityId);
                ValidateBindingTarget(
                    binding.EntityId,
                    binding.PresentationId,
                    i,
                    enemyBinding: false,
                    spawnMap,
                    staticSnapshot,
                    strict,
                    options,
                    stageId,
                    authoringAssetName,
                    presentation,
                    presentationPath,
                    issues);
            }

            foreach (var entityId in enemySpawnEntityIds)
            {
                if (enemyBindingsByEntityId.Contains(entityId))
                {
                    continue;
                }

                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                    "PresentationBinding.MissingEnemyBinding",
                    $"Enemy spawn EntityId={entityId} has no generated enemy presentation binding.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: "EnemyPresentationBindings",
                    expectedValue: $"EntityId={entityId}",
                    actualValue: "missing"));
            }

            foreach (var entityId in staticSpawnEntityIds)
            {
                if (staticBindingsByEntityId.Contains(entityId))
                {
                    continue;
                }

                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                    "PresentationBinding.MissingStaticBinding",
                    $"Static spawn EntityId={entityId} has no generated static entity presentation binding.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: "StaticEntityPresentationBindings",
                    expectedValue: $"EntityId={entityId}",
                    actualValue: "missing"));
            }
        }

        private static void ValidateBindingTarget(
            int entityId,
            string rawPresentationId,
            int bindingIndex,
            bool enemyBinding,
            IReadOnlyDictionary<int, StageSpawnDefinition> spawnMap,
            StageAuthoringPresentationCatalogSnapshot catalogSnapshot,
            bool strict,
            StageCatalogValidationOptions options,
            string stageId,
            string authoringAssetName,
            StagePresentationDefinition presentation,
            string presentationPath,
            ICollection<StageValidationIssue> issues)
        {
            var fieldPrefix = enemyBinding
                ? $"EnemyPresentationBindings[{bindingIndex}]"
                : $"StaticEntityPresentationBindings[{bindingIndex}]";
            var presentationId = enemyBinding
                ? EnemyPresentationCatalogResolver.NormalizePresentationId(rawPresentationId)
                : StaticEntityPresentationCatalogResolver.NormalizePresentationId(rawPresentationId);

            if (!spawnMap.TryGetValue(entityId, out var spawn))
            {
                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                    enemyBinding
                        ? "PresentationBinding.OrphanEnemyBinding"
                        : "PresentationBinding.OrphanStaticBinding",
                    $"{(enemyBinding ? "Enemy" : "Static")} presentation binding references missing EntityId={entityId}.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: $"{fieldPrefix}.EntityId",
                    expectedValue: enemyBinding ? "enemy spawn entity id" : "box/wall spawn entity id",
                    actualValue: entityId.ToString(),
                    presentationId: presentationId));
            }
            else if (!SpawnMatchesPresentationLane(spawn.Kind, ResolveBindingLane(enemyBinding)))
            {
                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    StagePresentationValidationSeverityPolicy.AlwaysError,
                    "PresentationBinding.BindingReferencesWrongKind",
                    $"{(enemyBinding ? "Enemy" : "Static")} presentation binding EntityId={entityId} points to spawn kind {spawn.Kind}.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: $"{fieldPrefix}.EntityId",
                    expectedValue: ResolveBindingExpectedSpawnKind(enemyBinding),
                    actualValue: spawn.Kind.ToString(),
                    presentationId: presentationId));
            }

            if (string.IsNullOrEmpty(presentationId))
            {
                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                    enemyBinding
                        ? "PresentationCatalog.EnemyPresentationIdEmpty"
                        : "PresentationCatalog.StaticPresentationIdEmpty",
                    $"{(enemyBinding ? "Enemy" : "Static")} presentation binding EntityId={entityId} must declare a non-empty PresentationId.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    presentation.name,
                    entityId,
                    fieldName: $"{fieldPrefix}.PresentationId",
                    expectedValue: "non-empty",
                    actualValue: rawPresentationId,
                    presentationId: presentationId));
                return;
            }

            if (catalogSnapshot.PresentationIds.Length == 0 ||
                catalogSnapshot.ContainsPresentationId(presentationId))
            {
                return;
            }

            issues.Add(StagePresentationCatalogIssueFactory.Create(
                StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                enemyBinding
                    ? "PresentationCatalog.EnemyPresentationIdMissing"
                    : "PresentationCatalog.StaticPresentationIdMissing",
                $"{(enemyBinding ? "Enemy" : "Static")} presentation binding EntityId={entityId} references unresolved PresentationId '{presentationId}'.",
                presentation,
                presentationPath,
                options.Timing,
                stageId,
                authoringAssetName,
                presentation.name,
                entityId,
                fieldName: $"{fieldPrefix}.PresentationId",
                expectedValue: enemyBinding
                    ? "id present in enemy presentation catalog"
                    : "id present in static entity presentation catalog",
                actualValue: rawPresentationId,
                presentationId: presentationId));
        }

        private static Dictionary<int, StageSpawnDefinition> BuildSpawnMap(StageDefinition gameplay)
        {
            var result = new Dictionary<int, StageSpawnDefinition>();
            var spawns = gameplay.Spawns;
            for (var i = 0; i < spawns.Length; i++)
            {
                result[spawns[i].EntityId] = spawns[i];
            }

            return result;
        }

        private static HashSet<int> BuildSpawnEntityIds(IReadOnlyList<StageSpawnDefinition> spawns)
        {
            var result = new HashSet<int>();
            if (spawns == null)
            {
                return result;
            }

            for (var i = 0; i < spawns.Count; i++)
            {
                result.Add(spawns[i].EntityId);
            }

            return result;
        }

        private static HashSet<int> BuildStaticSpawnEntityIds(StageDefinition gameplay)
        {
            var result = BuildSpawnEntityIds(gameplay.BoxSpawns);
            var walls = gameplay.WallSpawns;
            for (var i = 0; i < walls.Length; i++)
            {
                result.Add(walls[i].EntityId);
            }

            return result;
        }

        private static StageAuthoringPresentationLane ResolveBindingLane(bool enemyBinding)
        {
            return enemyBinding ? StageAuthoringPresentationLane.Enemy : StageAuthoringPresentationLane.Static;
        }

        private static bool SpawnMatchesPresentationLane(StageSpawnKind spawnKind, StageAuthoringPresentationLane lane)
        {
            return lane switch
            {
                StageAuthoringPresentationLane.Enemy => spawnKind == StageSpawnKind.Enemy,
                StageAuthoringPresentationLane.Static => spawnKind == StageSpawnKind.Box ||
                                                         spawnKind == StageSpawnKind.Wall,
                _ => false,
            };
        }

        private static string ResolveBindingExpectedSpawnKind(bool enemyBinding)
        {
            return enemyBinding ? "Enemy" : "Box or Wall";
        }
    }
}
