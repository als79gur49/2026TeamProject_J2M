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
                cells: SurfaceBeltSlotMapping.BuildCells(centerSlot),
                buttonRemainders: BuildButtonRemainders(snapshot));
        }

        private static SurfaceBeltButtonRemainderViewModel[] BuildButtonRemainders(SurfaceBeltSnapshot snapshot)
        {
            var source = snapshot.ButtonRemainders;
            var result = SurfaceBeltViewModel.CreateEmptyButtonRemainders();
            if (source == null)
            {
                return result;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var slot = SurfaceBeltSlotMapping.WrapSlot(item.SlotIndex);
                result[slot] = new SurfaceBeltButtonRemainderViewModel(
                    slot,
                    item.NormalRemaining,
                    item.MoonBlockOnlyRemaining);
            }

            return result;
        }
    }
}
