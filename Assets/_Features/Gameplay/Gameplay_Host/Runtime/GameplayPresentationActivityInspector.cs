namespace Game.Feature.Gameplay.Host
{
    /// <summary>
    /// Reads track activity so facade-level phase decisions stay thin.
    /// </summary>
    internal sealed class GameplayPresentationActivityInspector
    {
        private readonly GameplayPresentationTrackState _trackState;

        public GameplayPresentationActivityInspector(GameplayPresentationTrackState trackState)
        {
            _trackState = trackState ?? throw new System.ArgumentNullException(nameof(trackState));
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

            foreach (var pair in _trackState.JumpWindupRotationTracks)
            {
                if (pair.Value.HasClips)
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

            foreach (var pair in _trackState.OriginalViewMotionTracks)
            {
                if (pair.Value != null && !pair.Value.IsComplete)
                {
                    return true;
                }
            }

            foreach (var pair in _trackState.PlayerDeathDisplacementTracks)
            {
                if (pair.Value != null && pair.Value.IsAnimating)
                {
                    return true;
                }
            }

            if (_trackState.FlipInteractionResetRequests.Count > 0)
            {
                return true;
            }

            return false;
        }

        public bool HasActiveBlockingJumpLandingCompletion()
        {
            foreach (var entityId in _trackState.JumpLandingCompletionHoldEntityIds)
            {
                if (_trackState.JumpTracks.TryGetValue(entityId, out var jumpTrack) &&
                    jumpTrack.HasClip)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
