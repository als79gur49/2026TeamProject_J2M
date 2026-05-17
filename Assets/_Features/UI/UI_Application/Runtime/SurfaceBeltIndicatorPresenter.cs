using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class SurfaceBeltIndicatorPresenter
    {
        public SurfaceBeltViewModel ViewModel { get; } = new();

        public void Apply(SurfaceBeltSnapshot snapshot)
        {
            var centerSlot = snapshot.IsTransitioning
                ? snapshot.SourceSlotIndex
                : snapshot.CurrentSlotIndex;
            ViewModel.SetState(
                visible: true,
                centerSlotIndex: centerSlot,
                sourceSlotIndex: snapshot.SourceSlotIndex,
                destinationSlotIndex: snapshot.DestinationSlotIndex,
                direction: snapshot.Direction,
                isTransitioning: snapshot.IsTransitioning,
                transitionSequenceId: snapshot.TransitionSequenceId,
                cells: SurfaceBeltSlotMapping.BuildCells(centerSlot));
        }
    }
}
