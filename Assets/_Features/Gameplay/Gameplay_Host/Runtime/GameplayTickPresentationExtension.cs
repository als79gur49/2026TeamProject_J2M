using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayTickPresentationExtensionContext
    {
        public GameplayTickPresentationExtensionContext(
            TickResult result,
            CubeTopologyState topology,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            EnemyPresentationCatalog enemyPresentationCatalog = null,
            EnemyPresentationBinding[] enemyPresentationBindings = null,
            GameplayTimingProfile timingProfile = null)
        {
            Result = result;
            Topology = topology;
            StateStore = stateStore;
            Projector = projector;
            EnemyPresentationCatalog = enemyPresentationCatalog;
            EnemyPresentationBindings = enemyPresentationBindings ?? System.Array.Empty<EnemyPresentationBinding>();
            TimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
        }

        public TickResult Result { get; }

        public CubeTopologyState Topology { get; }

        public GameplayPresentationStateStore StateStore { get; }

        public GameplayCubeProjector Projector { get; }

        public EnemyPresentationCatalog EnemyPresentationCatalog { get; }

        public EnemyPresentationBinding[] EnemyPresentationBindings { get; }

        public GameplayTimingProfile TimingProfile { get; }
    }

    public interface IGameplayTickPresentationExtension
    {
        void ResetSession();

        void Present(in GameplayTickPresentationExtensionContext context);

        void UpdatePresentation(float deltaTime);

        void HardCleanup();
    }

    public interface IGameplayOutputCameraPresentationExtension
    {
        void ConfigureOutputCamera(Camera outputCamera, Transform localSpaceRoot);
    }

    public interface IGameplayPresentationMigrationGate
    {
        bool SuppressLegacyPlayerDamageHitEffects { get; }

        bool SuppressLegacyBoxDestroyShrinkEffects { get; }

        bool SuppressLegacyItemConsumeEffects { get; }

        bool SuppressLegacyEnemyDeathEffects { get; }

        bool SuppressLegacyFlipDestroySelfEffects { get; }

        bool SuppressLegacyUtilityWindupVfx { get; }

        bool SuppressLegacyFrontFaceShieldActiveVfx { get; }

        bool SuppressLegacyFrontFaceShieldBlockVfx { get; }
    }
}
