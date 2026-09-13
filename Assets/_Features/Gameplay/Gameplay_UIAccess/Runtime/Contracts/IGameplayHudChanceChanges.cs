using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Contracts
{
    // Optional, memory-only change observation. UI never subscribes to the save layer.
    public interface IGameplayHudChanceChanges
    {
        bool TryGetChanceRevision(out long revision);
        bool IsChanceDisplayUpdating { get; }
        GameplayPlayerHudReadModel ReadChance(out long revision);
    }

    public interface IGameplayHudPendingRefresh
    {
        void FlushPendingChanceChanges();
    }
}
