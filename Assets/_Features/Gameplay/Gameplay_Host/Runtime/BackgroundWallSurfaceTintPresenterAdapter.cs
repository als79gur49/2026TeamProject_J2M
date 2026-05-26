using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class BackgroundWallSurfaceTintPresenterAdapter : IDisposable
    {
        private readonly BackgroundWallSurfaceTintAuthoring _authoring;
        private readonly GameplayTickViewPresenter _presenter;
        private bool _isDisposed;

        public BackgroundWallSurfaceTintPresenterAdapter(
            BackgroundWallSurfaceTintAuthoring authoring,
            GameplayTickViewPresenter presenter)
        {
            _authoring = authoring;
            _presenter = presenter;

            if (_authoring == null || _presenter == null)
            {
                return;
            }

            _presenter.PresentationStateChanged += HandlePresentationStateChanged;
            ApplyCurrentTint();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_presenter != null)
            {
                _presenter.PresentationStateChanged -= HandlePresentationStateChanged;
            }

            _isDisposed = true;
        }

        internal static FaceId ResolveTintFace(
            CubeTopologyState currentTopology,
            TopologyTransitionVisualState transitionState)
        {
            return transitionState.IsActive
                ? transitionState.DestinationTopology.BottomFace
                : currentTopology.BottomFace;
        }

        private void HandlePresentationStateChanged()
        {
            ApplyCurrentTint();
        }

        private void ApplyCurrentTint()
        {
            if (_authoring == null || _presenter == null)
            {
                return;
            }

            var face = ResolveTintFace(
                _presenter.CurrentTopology,
                _presenter.CurrentTopologyTransitionVisualState);
            _authoring.ApplyFaceTint(face);
        }
    }
}
