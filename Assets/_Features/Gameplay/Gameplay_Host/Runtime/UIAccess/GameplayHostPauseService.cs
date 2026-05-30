using System;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.UIAccess.Contracts;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostPauseService : IGameplayPauseService
    {
        private readonly GameplayInputHost _inputHost;
        private readonly GameplayTickViewPresenter _presenter;
        private int _pauseDepth;

        public GameplayHostPauseService(GameplayInputHost inputHost, GameplayTickViewPresenter presenter = null)
        {
            _inputHost = inputHost ?? throw new ArgumentNullException(nameof(inputHost));
            _presenter = presenter;
        }

        public event Action<bool> PauseChanged;

        public bool IsPaused => _pauseDepth > 0;

        internal int PauseDepth => _pauseDepth;

        public void Pause()
        {
            var wasPaused = IsPaused;
            _pauseDepth++;
            if (wasPaused)
            {
                return;
            }

            _inputHost.ClearPendingUiInput();
            _inputHost.SetSimulationPaused(true);
            _presenter?.SetPresentationPaused(true);
            PauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (_pauseDepth <= 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"{nameof(GameplayHostPauseService)}.{nameof(Resume)} ignored because there is no active pause ownership.");
                return;
            }

            _pauseDepth--;
            if (_pauseDepth > 0)
            {
                return;
            }

            _presenter?.SetPresentationPaused(false);
            _inputHost.SetSimulationPaused(false);
            PauseChanged?.Invoke(false);
        }

        public void Toggle()
        {
            if (IsPaused)
            {
                Resume();
                return;
            }

            Pause();
        }
    }
}
