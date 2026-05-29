using Game.Feature.Gameplay.AudioPolicy;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplaySfxPolicyCatalog
    {
        public static AudioVoicePolicy Resolve(string debugTag)
        {
            debugTag ??= string.Empty;
            if (debugTag == "PlayerDamage")
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.PlayerCritical,
                    priority: 100,
                    maxVoicesGlobal: 2,
                    maxVoicesPerOwner: 1,
                    cooldownSecondsGlobal: 0f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            if (debugTag == "EnemyDamage")
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.EnemyDamage,
                    priority: 50,
                    maxVoicesGlobal: 2,
                    maxVoicesPerOwner: 1,
                    cooldownSecondsGlobal: 0f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            if (debugTag == "Move")
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.EnemyMovement,
                    priority: 15,
                    maxVoicesGlobal: 3,
                    maxVoicesPerOwner: 1,
                    cooldownSecondsGlobal: 0f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            if (debugTag == "StationaryActive")
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.GenericGameplay,
                    priority: 40,
                    maxVoicesGlobal: 3,
                    maxVoicesPerOwner: 1,
                    cooldownSecondsGlobal: 0f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            if (debugTag == "ProjectileImpact")
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.ProjectileImpact,
                    priority: 60,
                    maxVoicesGlobal: 3,
                    maxVoicesPerOwner: 1,
                    cooldownSecondsGlobal: 0f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            if (debugTag == "TileFeatureOnBurst" ||
                debugTag == "TileFeatureOffBurst" ||
                debugTag == "DestroyTileActivated" ||
                debugTag == "DestroyTileDeactivated" ||
                debugTag == "BarricadeActivated" ||
                debugTag == "BarricadeDeactivated")
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.TileFeatureToggle,
                    priority: 25,
                    maxVoicesGlobal: 2,
                    maxVoicesPerOwner: 0,
                    cooldownSecondsGlobal: 0.05f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            if (debugTag.StartsWith("Action:", System.StringComparison.Ordinal))
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.GameplayAction,
                    priority: 60,
                    maxVoicesGlobal: 0,
                    maxVoicesPerOwner: 0,
                    cooldownSecondsGlobal: 0f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0.01f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            if (debugTag == "FlipLanding" ||
                debugTag == "BoxSlideSolidStop" ||
                debugTag == "BoxSlideStarted")
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.BlockImpact,
                    priority: 35,
                    maxVoicesGlobal: 3,
                    maxVoicesPerOwner: 1,
                    cooldownSecondsGlobal: 0f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            if (debugTag == "WalkStep" ||
                debugTag == "TopologyTransitionBlocked")
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.PlayerLocomotion,
                    priority: 20,
                    maxVoicesGlobal: 2,
                    maxVoicesPerOwner: 1,
                    cooldownSecondsGlobal: 0f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            if (debugTag == "Activated" ||
                debugTag == "Expired" ||
                debugTag == "LockedBox")
            {
                return new AudioVoicePolicy(
                    AudioVoiceGroupId.GravityField,
                    priority: 30,
                    maxVoicesGlobal: 2,
                    maxVoicesPerOwner: 1,
                    cooldownSecondsGlobal: 0f,
                    cooldownSecondsPerOwner: 0f,
                    duplicateWindowSeconds: 0f,
                    overflowMode: VoiceOverflowMode.DropNewest);
            }

            return new AudioVoicePolicy(
                AudioVoiceGroupId.GenericGameplay,
                priority: 40,
                maxVoicesGlobal: 3,
                maxVoicesPerOwner: 1,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);
        }
    }
}
