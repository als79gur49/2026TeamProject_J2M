using System;
using Game.Feature.UI.Application;

namespace Game.Feature.UI.Composition
{
    internal sealed class MainMenuSettingsPortAdapter : IMainMenuSettingsPort
    {
        private readonly MainMenuSettingsOverlayController overlayController;

        public MainMenuSettingsPortAdapter(MainMenuSettingsOverlayController overlayController)
        {
            this.overlayController = overlayController ?? throw new ArgumentNullException(nameof(overlayController));
        }

        public void OpenSettings()
        {
            overlayController.Open();
        }
    }
}
