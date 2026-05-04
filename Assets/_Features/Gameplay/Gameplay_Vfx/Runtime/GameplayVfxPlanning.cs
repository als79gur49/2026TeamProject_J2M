namespace Game.Feature.Gameplay.Vfx
{
    public interface IGameplayVfxFamilyRequestPlanner
    {
        GameplayVfxFamily Family { get; }

        void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder);
    }

    public readonly struct GameplayVfxPlanningContext
    {
        public GameplayVfxPlanningContext(int tickIndex)
        {
            TickIndex = tickIndex;
        }

        public int TickIndex { get; }
    }

    public sealed class PlayerVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Player;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }

    public sealed class BoxVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Box;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }

    public sealed class EnemyVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Enemy;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }

    public sealed class TileFeatureVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.TileFeature;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }

    public sealed class TerrainVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Terrain;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }

    public sealed class ProjectileVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Projectile;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }

    public sealed class ObjectiveStageVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.ObjectiveStage;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }
}
