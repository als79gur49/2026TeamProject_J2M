using System;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.UIAccess.Contracts;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostPauseService : IGameplayPauseService
    {
        private readonly GameplayInputHost _inputHost;
        private readonly GameplayTickViewPresenter _presenter;

        public GameplayHostPauseService(GameplayInputHost inputHost, GameplayTickViewPresenter presenter = null)
        {
            _inputHost = inputHost ?? throw new ArgumentNullException(nameof(inputHost));
            _presenter = presenter;
        }

        public event Action<bool> PauseChanged;

        public bool IsPaused { get; private set; }

        public void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            IsPaused = true;
            _inputHost.ClearPendingUiInput();
            _inputHost.SetSimulationPaused(true);
            _presenter?.SetPresentationPaused(true);
            PauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            IsPaused = false;
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
