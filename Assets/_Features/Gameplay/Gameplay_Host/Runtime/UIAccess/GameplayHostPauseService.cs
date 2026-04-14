using System;
using Game.Feature.Gameplay.UIAccess.Contracts;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostPauseService : IGameplayPauseService
    {
        private readonly GameplayInputHost _inputHost;

        public GameplayHostPauseService(GameplayInputHost inputHost)
        {
            _inputHost = inputHost ?? throw new ArgumentNullException(nameof(inputHost));
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
            PauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            IsPaused = false;
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
