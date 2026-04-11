namespace Game.Feature.Gameplay.Host
{
    /// <summary>
    /// Reads track and transient-effect activity so facade-level phase decisions stay thin.
    /// </summary>
    internal sealed class GameplayPresentationActivityInspector
    {
        private readonly GameplayPresentationTrackState _trackState;
        private readonly GameplayTransientEffectPresenter _transientEffectPresenter;

        public GameplayPresentationActivityInspector(
            GameplayPresentationTrackState trackState,
            GameplayTransientEffectPresenter transientEffectPresenter)
        {
            _trackState = trackState ?? throw new System.ArgumentNullException(nameof(trackState));
            _transientEffectPresenter =
                transientEffectPresenter ?? throw new System.ArgumentNullException(nameof(transientEffectPresenter));
        }

        public bool HasActiveEntityPresentationClips()
        {
            foreach (var pair in _trackState.LocalMotionTracks)
            {
                if (pair.Value.HasClips)
                {
                    return true;
                }
            }

            foreach (var pair in _trackState.VisibilityTracks)
            {
                if (pair.Value.IsActive)
                {
                    return true;
                }
            }

            foreach (var pair in _trackState.JumpTracks)
            {
                if (pair.Value.HasClip)
                {
                    return true;
                }
            }

            foreach (var pair in _trackState.FlipInteractionTracks)
            {
                if (!pair.Value.IsComplete)
                {
                    return true;
                }
            }

            if (_trackState.FlipInteractionResetRequests.Count > 0)
            {
                return true;
            }

            return _transientEffectPresenter.HasActiveEffects;
        }
    }
}
