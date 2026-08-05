using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
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
        private readonly Func<StageNavigationRequest, bool> _tryLaunchStage;
        private readonly ScreenController _screenController;
        private bool _isArmed;
        private bool _launched;
        private StageNavigationRequest _nextStageRequest = StageNavigationRequest.None;

        public StageResultAutoNextDriver(
            ScreenController screenController,
            PopupController popupController,
            IStageLaunchRouter stageLaunchRouter)
            : this(
                screenController,
                popupController,
                request =>
                {
                    stageLaunchRouter.Launch(request);
                    return true;
                })
        {
            if (stageLaunchRouter == null)
            {
                throw new ArgumentNullException(nameof(stageLaunchRouter));
            }
        }

        public StageResultAutoNextDriver(
            ScreenController screenController,
            PopupController popupController,
            Func<StageNavigationRequest, bool> tryLaunchStage)
        {
            _screenController = screenController ?? throw new ArgumentNullException(nameof(screenController));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _tryLaunchStage = tryLaunchStage ?? throw new ArgumentNullException(nameof(tryLaunchStage));
            _screenController.StateChanged += HandleScreenStateChanged;
            TerminalSessionRegistry.Changed += HandleTerminalSessionChanged;
        }

        public float RemainingSeconds { get; private set; }

        public int LaunchCount { get; private set; }

        public bool IsArmed => _isArmed;

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

            RemainingSeconds -= deltaTime;
            if (RemainingSeconds > 0f)
            {
                return;
            }

            LaunchOnce();
        }

        public void Dispose()
        {
            _screenController.StateChanged -= HandleScreenStateChanged;
            TerminalSessionRegistry.Changed -= HandleTerminalSessionChanged;
        }

        private void HandleScreenStateChanged()
        {
            TryArmFromProductionRoot();
        }

        private void HandleTerminalSessionChanged(TerminalSessionSnapshot snapshot)
        {
            if (snapshot.IsActive)
            {
                _isArmed = false;
                return;
            }

            TryArmFromProductionRoot();
        }

        private void TryArmFromProductionRoot()
        {
            var currentEntry = _screenController.CurrentEntry;
            if (!currentEntry.HasValue ||
                currentEntry.Value.ScreenId != ScreenId.StageResult ||
                currentEntry.Value.Payload is not StageResultScreenPayload payload ||
                !payload.NextStageRequest.IsValid)
            {
                Disarm();
                return;
            }

            _nextStageRequest = payload.NextStageRequest;
            if (TerminalSessionRegistry.IsActive)
            {
                _isArmed = false;
                _launched = false;
                RemainingSeconds = 0f;
                return;
            }

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

            if (!_tryLaunchStage(_nextStageRequest))
            {
                return;
            }

            _launched = true;
            LaunchCount++;
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
