using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Audio
{
    // Core required gameplay one-shot semantic ids only.
    // Push/flip/action-specific SFX must move through the action-audio profile layer by default.
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

    public enum GameplayAudioSemanticFamily
    {
        DamageOneShot = 1,
        EntityExitOneShot = 2,
        Locomotion = 3,
        JumpLoop = 4,
        ActionLoop = 5,
        AmbientGameplayBed = 6,
        UiInteraction = 7,
        BgmFlow = 8,
    }

    public readonly struct GameplayAudioSemanticDescriptor
    {
        public GameplayAudioSemanticDescriptor(
            GameplayAudioSemanticId semanticId,
            GameplayAudioSemanticFamily family,
            bool isRequiredForHostOneShotV1)
        {
            SemanticId = semanticId;
            Family = family;
            IsRequiredForHostOneShotV1 = isRequiredForHostOneShotV1;
        }

        public GameplayAudioSemanticId SemanticId { get; }

        public GameplayAudioSemanticFamily Family { get; }

        public bool IsRequiredForHostOneShotV1 { get; }
    }

    public static class GameplayAudioSemanticCatalog
    {
        // This governed descriptor set stays intentionally small and exact because it is the
        // shared core one-shot contract for damage and entity-exit reactions, not the growing
        // vocabulary for every gameplay action sound.
        private static readonly GameplayAudioSemanticDescriptor[] GovernedDescriptors =
        {
            new(GameplayAudioSemanticId.PlayerDamage, GameplayAudioSemanticFamily.DamageOneShot, isRequiredForHostOneShotV1: true),
            new(GameplayAudioSemanticId.EnemyDamage, GameplayAudioSemanticFamily.DamageOneShot, isRequiredForHostOneShotV1: true),
            new(GameplayAudioSemanticId.EntityExitItemConsume, GameplayAudioSemanticFamily.EntityExitOneShot, isRequiredForHostOneShotV1: true),
            new(GameplayAudioSemanticId.EntityExitBoxDestroy, GameplayAudioSemanticFamily.EntityExitOneShot, isRequiredForHostOneShotV1: true),
            new(GameplayAudioSemanticId.EntityExitEnemyDeath, GameplayAudioSemanticFamily.EntityExitOneShot, isRequiredForHostOneShotV1: true),
            new(GameplayAudioSemanticId.EntityExitOutOfBounds, GameplayAudioSemanticFamily.EntityExitOneShot, isRequiredForHostOneShotV1: true),
        };

        private static readonly GameplayAudioSemanticFamily[] ApprovedHostOneShotFamilies =
        {
            GameplayAudioSemanticFamily.DamageOneShot,
            GameplayAudioSemanticFamily.EntityExitOneShot,
        };

        private static readonly GameplayAudioSemanticId[] GovernedSemanticIds = BuildGovernedSemanticIds();
        private static readonly GameplayAudioSemanticId[] RequiredOneShotIds = BuildRequiredOneShotIds();

        public static IReadOnlyList<GameplayAudioSemanticDescriptor> GovernedSemantics => GovernedDescriptors;

        public static IReadOnlyList<GameplayAudioSemanticId> GovernedSemanticIdsV1 => GovernedSemanticIds;

        public static IReadOnlyList<GameplayAudioSemanticFamily> ApprovedHostOneShotFamiliesV1 => ApprovedHostOneShotFamilies;

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

        public static GameplayAudioSemanticDescriptor GetDescriptor(GameplayAudioSemanticId semanticId)
        {
            if (semanticId == GameplayAudioSemanticId.None)
            {
                throw new ArgumentException("Gameplay audio semantic id cannot be None.", nameof(semanticId));
            }

            for (var i = 0; i < GovernedDescriptors.Length; i++)
            {
                if (GovernedDescriptors[i].SemanticId == semanticId)
                {
                    return GovernedDescriptors[i];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(semanticId), semanticId, "Unsupported gameplay audio semantic id.");
        }

        public static GameplayAudioSemanticFamily GetFamily(GameplayAudioSemanticId semanticId)
        {
            return GetDescriptor(semanticId).Family;
        }

        public static bool IsApprovedHostOneShotFamily(GameplayAudioSemanticFamily family)
        {
            for (var i = 0; i < ApprovedHostOneShotFamilies.Length; i++)
            {
                if (ApprovedHostOneShotFamilies[i] == family)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsAllowedInHostOneShotV1(GameplayAudioSemanticId semanticId)
        {
            return IsApprovedHostOneShotFamily(GetFamily(semanticId));
        }

        public static bool IsRequiredForHostOneShotV1(GameplayAudioSemanticId semanticId)
        {
            return GetDescriptor(semanticId).IsRequiredForHostOneShotV1;
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

        private static GameplayAudioSemanticId[] BuildGovernedSemanticIds()
        {
            var semanticIds = new GameplayAudioSemanticId[GovernedDescriptors.Length];
            for (var i = 0; i < GovernedDescriptors.Length; i++)
            {
                semanticIds[i] = GovernedDescriptors[i].SemanticId;
            }

            return semanticIds;
        }

        private static GameplayAudioSemanticId[] BuildRequiredOneShotIds()
        {
            var requiredCount = 0;
            for (var i = 0; i < GovernedDescriptors.Length; i++)
            {
                if (GovernedDescriptors[i].IsRequiredForHostOneShotV1)
                {
                    requiredCount++;
                }
            }

            var semanticIds = new GameplayAudioSemanticId[requiredCount];
            var nextIndex = 0;
            for (var i = 0; i < GovernedDescriptors.Length; i++)
            {
                if (!GovernedDescriptors[i].IsRequiredForHostOneShotV1)
                {
                    continue;
                }

                semanticIds[nextIndex++] = GovernedDescriptors[i].SemanticId;
            }

            return semanticIds;
        }
    }

    public readonly struct GameplayAudioRequest
    {
        public GameplayAudioRequest(
            GameplayAudioSemanticId semanticId,
            int? ownerEntityId,
            in AudioPlaybackContext context,
            float delaySeconds = 0f)
        {
            SemanticId = semanticId;
            OwnerEntityId = ownerEntityId;
            Context = context;
            DelaySeconds = Math.Max(0f, delaySeconds);
        }

        public GameplayAudioSemanticId SemanticId { get; }

        public int? OwnerEntityId { get; }

        public AudioPlaybackContext Context { get; }

        public float DelaySeconds { get; }
    }
}
