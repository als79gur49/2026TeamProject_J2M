using System;

namespace Game.Feature.Gameplay.UIAccess.Contracts
{
    public interface IGameplayPauseService
    {
        event Action<bool> PauseChanged;

        bool IsPaused { get; }

        void Pause();

        void Resume();

        void Toggle();
    }
}
