using Game.Feature.Gameplay.Host;
using Game.Shared.AudioContracts;
using System;
using System.Collections.Generic;

namespace Game.Feature.Stages.Editor
{
    public static class LegacyStagePresentationEditorBridge
    {
        public static StagePresentationResolvedData Resolve(
            StageDefinition stageDefinition,
            EnemyPresentationCatalog enemyPresentationCatalog = null,
            StaticEntityPresentationCatalog staticEntityPresentationCatalog = null)
        {
            if (stageDefinition == null)
            {
                return StagePresentationAssembler.EmptyResolvedData;
            }

            var spawns = stageDefinition.Spawns;
            return new StagePresentationResolvedData(
                stageDefinition.name,
                string.Empty,
                null,
                null,
                StageBgmReference.None,
                enemyPresentationCatalog,
                BuildEnemyBindings(spawns),
                staticEntityPresentationCatalog,
                BuildStaticBindings(spawns),
                "Stage Cleared",
                string.Empty,
                string.Empty,
                "Continue");
        }

        private static EnemyPresentationBinding[] BuildEnemyBindings(IReadOnlyList<StageSpawnDefinition> spawns)
        {
            var bindings = new List<EnemyPresentationBinding>();

            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                if (spawn.Kind != StageSpawnKind.Enemy)
                {
                    continue;
                }

                var presentationId = NormalizePresentationId(spawn.PresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    continue;
                }

                bindings.Add(new EnemyPresentationBinding
                {
                    EntityId = spawn.EntityId,
                    PresentationId = presentationId,
                });
            }

            bindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            return bindings.ToArray();
        }

        private static StaticEntityPresentationBinding[] BuildStaticBindings(IReadOnlyList<StageSpawnDefinition> spawns)
        {
            var bindings = new List<StaticEntityPresentationBinding>();

            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                if (spawn.Kind != StageSpawnKind.Box &&
                    spawn.Kind != StageSpawnKind.Wall)
                {
                    continue;
                }

                var presentationId = NormalizePresentationId(spawn.PresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    continue;
                }

                bindings.Add(new StaticEntityPresentationBinding
                {
                    EntityId = spawn.EntityId,
                    PresentationId = presentationId,
                });
            }

            bindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            return bindings.ToArray();
        }

        private static string NormalizePresentationId(string presentationId)
        {
            return string.IsNullOrWhiteSpace(presentationId)
                ? string.Empty
                : presentationId.Trim();
        }
    }
}
