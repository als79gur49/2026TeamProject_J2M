using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class PlayerStatusPresenter
    {
        public PlayerStatusViewModel ViewModel { get; } = new();

        public void Apply(
            UITickSlice tick,
            UIInteractionSlice _,
            UIPlayerActionSlice player)
        {
            ViewModel.SetState(
                player.Facing.ToString(),
                string.Empty);
        }
    }
}
