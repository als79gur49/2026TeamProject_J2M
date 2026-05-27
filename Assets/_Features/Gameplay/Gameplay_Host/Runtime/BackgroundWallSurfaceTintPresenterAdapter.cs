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
        private readonly BackgroundWallSurfaceTintNoiseController _noiseController;
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

            _noiseController = new BackgroundWallSurfaceTintNoiseController(_authoring);
            _presenter.PresentationStateChanged += HandlePresentationStateChanged;
            _presenter.PresentationAdvanced += HandlePresentationAdvanced;
            ApplyCurrentTint(initialApply: true);
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
                _presenter.PresentationAdvanced -= HandlePresentationAdvanced;
            }

            _noiseController?.Reset();
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
            ApplyCurrentTint(initialApply: false);
        }

        private void HandlePresentationAdvanced(float deltaTime)
        {
            _noiseController?.Advance(deltaTime);
        }

        private void ApplyCurrentTint(bool initialApply)
        {
            if (_authoring == null || _presenter == null || _noiseController == null)
            {
                return;
            }

            var transitionState = _presenter.CurrentTopologyTransitionVisualState;
            var face = ResolveTintFace(
                _presenter.CurrentTopology,
                transitionState);
            if (initialApply)
            {
                _noiseController.ApplyInitialStableFace(face);
                return;
            }

            _noiseController.ObservePresentationFace(
                face,
                transitionState.IsActive,
                transitionState.DurationSeconds);
        }
    }
}
