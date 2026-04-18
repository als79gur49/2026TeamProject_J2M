using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Audio
{
    public enum GameplayAudioSemanticId
    {
        None = 0,
        PlayerDamage = 1,
        EnemyDamage = 2,
        EntityExitItemConsume = 3,
        EntityExitBoxDestroy = 4,
        EntityExitEnemyDeath = 5,
        EntityExitOutOfBounds = 6,
    }

    public static class GameplayAudioSemanticCatalog
    {
        private static readonly GameplayAudioSemanticId[] RequiredOneShotIds =
        {
            GameplayAudioSemanticId.PlayerDamage,
            GameplayAudioSemanticId.EnemyDamage,
            GameplayAudioSemanticId.EntityExitItemConsume,
            GameplayAudioSemanticId.EntityExitBoxDestroy,
            GameplayAudioSemanticId.EntityExitEnemyDeath,
            GameplayAudioSemanticId.EntityExitOutOfBounds,
        };

        public static IReadOnlyList<GameplayAudioSemanticId> RequiredOneShotV1 => RequiredOneShotIds;

        public static string Format(GameplayAudioSemanticId semanticId)
        {
            return semanticId switch
            {
                GameplayAudioSemanticId.None => nameof(GameplayAudioSemanticId.None),
                GameplayAudioSemanticId.PlayerDamage => nameof(GameplayAudioSemanticId.PlayerDamage),
                GameplayAudioSemanticId.EnemyDamage => nameof(GameplayAudioSemanticId.EnemyDamage),
                GameplayAudioSemanticId.EntityExitItemConsume => nameof(GameplayAudioSemanticId.EntityExitItemConsume),
                GameplayAudioSemanticId.EntityExitBoxDestroy => nameof(GameplayAudioSemanticId.EntityExitBoxDestroy),
                GameplayAudioSemanticId.EntityExitEnemyDeath => nameof(GameplayAudioSemanticId.EntityExitEnemyDeath),
                GameplayAudioSemanticId.EntityExitOutOfBounds => nameof(GameplayAudioSemanticId.EntityExitOutOfBounds),
                _ => throw new ArgumentOutOfRangeException(nameof(semanticId), semanticId, "Unsupported gameplay audio semantic id."),
            };
        }

        public static bool TryResolveExitSemantic(
            TickEntityExitCause exitCause,
            out GameplayAudioSemanticId semanticId)
        {
            if (exitCause == TickEntityExitCause.ItemConsume)
            {
                semanticId = GameplayAudioSemanticId.EntityExitItemConsume;
                return true;
            }

            if (exitCause == TickEntityExitCause.BoxDestroy ||
                exitCause == TickEntityExitCause.DestroyedByImpact)
            {
                semanticId = GameplayAudioSemanticId.EntityExitBoxDestroy;
                return true;
            }

            if (exitCause == TickEntityExitCause.EnemyDeath ||
                exitCause == TickEntityExitCause.Killed)
            {
                semanticId = GameplayAudioSemanticId.EntityExitEnemyDeath;
                return true;
            }

            if (exitCause == TickEntityExitCause.OutOfBounds)
            {
                semanticId = GameplayAudioSemanticId.EntityExitOutOfBounds;
                return true;
            }

            semanticId = GameplayAudioSemanticId.None;
            return false;
        }
    }

    public readonly struct GameplayAudioRequest
    {
        public GameplayAudioRequest(
            GameplayAudioSemanticId semanticId,
            int? ownerEntityId,
            in AudioPlaybackContext context)
        {
            SemanticId = semanticId;
            OwnerEntityId = ownerEntityId;
            Context = context;
        }

        public GameplayAudioSemanticId SemanticId { get; }

        public int? OwnerEntityId { get; }

        public AudioPlaybackContext Context { get; }
    }
}
