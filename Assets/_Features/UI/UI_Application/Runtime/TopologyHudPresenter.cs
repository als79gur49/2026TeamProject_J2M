using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class TopologyHudPresenter
    {
        private bool _hasPrevious;
        private UITopologySlice _previous;
        private int _sequenceId;

        public SurfaceIndicatorViewModel ViewModel { get; } = new();

        public void Apply(UITopologySlice topology)
        {
            var hint = BuildAnimationHint(topology);
            ViewModel.SetState(
                topology.CurrentFaceLabel,
                topology.CurrentFaceIndex,
                topology.IsTransitionActive,
                topology.SourceFaceLabel,
                topology.DestinationFaceLabel,
                topology.Progress01,
                hint);

            _previous = topology;
            _hasPrevious = true;
        }

        private SurfaceIndicatorAnimationHint BuildAnimationHint(UITopologySlice next)
        {
            if (!_hasPrevious)
            {
                return SurfaceIndicatorAnimationHint.None;
            }

            var completedTransition = _previous.IsTransitionActive &&
                !next.IsTransitionActive &&
                _previous.CurrentFaceIndex != next.CurrentFaceIndex;
            if (!completedTransition)
            {
                return SurfaceIndicatorAnimationHint.None;
            }

            _sequenceId++;
            return new SurfaceIndicatorAnimationHint(
                true,
                next.CurrentFaceIndex,
                _sequenceId);
        }
    }
}
