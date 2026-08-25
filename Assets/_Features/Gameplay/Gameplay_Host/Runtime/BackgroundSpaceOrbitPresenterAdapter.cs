using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class BackgroundSpaceOrbitPresenterAdapter : IDisposable
    {
        private readonly BackgroundSpaceOrbitAuthoring _authoring;
        private readonly GameplayTickViewPresenter _presenter;
        private bool _isDisposed;

        public BackgroundSpaceOrbitPresenterAdapter(
            BackgroundSpaceOrbitAuthoring authoring,
            GameplayTickViewPresenter presenter)
        {
            _authoring = authoring;
            _presenter = presenter;

            if (_authoring == null || _presenter == null)
            {
                return;
            }

            _authoring.Initialize();
            _presenter.PresentationAdvanced += HandlePresentationAdvanced;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_presenter != null)
            {
                _presenter.PresentationAdvanced -= HandlePresentationAdvanced;
            }

            _authoring?.ResetRuntimeState();
            _isDisposed = true;
        }

        internal static float ResolveTopologyResponse(TopologyTransitionVisualState transitionState)
        {
            return transitionState.IsActive
                ? Mathf.Clamp01(transitionState.AngularVelocityNormalized)
                : 0f;
        }

        internal static BackgroundSpaceOrbitAuthoring ResolveSingleAuthoring(GameObject backgroundInstance)
        {
            if (backgroundInstance == null)
            {
                return null;
            }

            var authorings = backgroundInstance.GetComponentsInChildren<BackgroundSpaceOrbitAuthoring>(true);
            if (authorings.Length == 0)
            {
                return null;
            }

            if (authorings.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Background '{backgroundInstance.name}' contains {authorings.Length} " +
                    $"{nameof(BackgroundSpaceOrbitAuthoring)} components; exactly one is supported.");
            }

            return authorings[0];
        }

        private void HandlePresentationAdvanced(float deltaTime)
        {
            if (_authoring == null || _presenter == null)
            {
                return;
            }

            _authoring.Advance(
                deltaTime,
                ResolveTopologyResponse(_presenter.CurrentTopologyTransitionVisualState));
        }
    }
}
