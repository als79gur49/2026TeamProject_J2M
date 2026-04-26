using System;
using Game.Feature.Stages;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal sealed class StageResultAutoNextDriver : IDisposable
    {
        public const float DefaultCountdownSeconds = 3f;

        private readonly PopupController _popupController;
        private readonly IStageLaunchRouter _stageLaunchRouter;
        private readonly ScreenController _screenController;
        private bool _isArmed;
        private bool _launched;
        private StageNavigationRequest _nextStageRequest = StageNavigationRequest.None;

        public StageResultAutoNextDriver(
            ScreenController screenController,
            PopupController popupController,
            IStageLaunchRouter stageLaunchRouter)
        {
            _screenController = screenController ?? throw new ArgumentNullException(nameof(screenController));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _screenController.ScreenTransitioned += HandleScreenTransitioned;
        }

        public float RemainingSeconds { get; private set; }

        public int LaunchCount { get; private set; }

        public bool IsArmed => _isArmed;

        public bool IsPausedByRewardPopup => _isArmed && _popupController.Contains(PopupId.Reward);

        public void Tick(float deltaTime)
        {
            if (!_isArmed || _launched)
            {
                return;
            }

            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (_popupController.Contains(PopupId.Reward))
            {
                return;
            }

            RemainingSeconds -= deltaTime;
            if (RemainingSeconds > 0f)
            {
                return;
            }

            LaunchOnce();
        }

        public void Dispose()
        {
            _screenController.ScreenTransitioned -= HandleScreenTransitioned;
        }

        private void HandleScreenTransitioned(ScreenTransitionedEvent transitionEvent)
        {
            if (!transitionEvent.CurrentEntry.HasValue ||
                transitionEvent.CurrentEntry.Value.ScreenId != ScreenId.StageResult ||
                transitionEvent.CurrentEntry.Value.Payload is not StageResultScreenPayload payload ||
                !payload.NextStageRequest.IsValid)
            {
                Disarm();
                return;
            }

            _nextStageRequest = payload.NextStageRequest;
            RemainingSeconds = DefaultCountdownSeconds;
            _isArmed = true;
            _launched = false;
        }

        private void LaunchOnce()
        {
            if (_launched || !_nextStageRequest.IsValid)
            {
                return;
            }

            _launched = true;
            LaunchCount++;
            _stageLaunchRouter.Launch(_nextStageRequest);
        }

        private void Disarm()
        {
            _isArmed = false;
            _launched = false;
            RemainingSeconds = 0f;
            _nextStageRequest = StageNavigationRequest.None;
        }
    }
}
