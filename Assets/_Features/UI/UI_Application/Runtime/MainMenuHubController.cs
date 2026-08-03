using System;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public sealed class MainMenuHubController
    {
        private readonly IApplicationQuitPort _applicationQuitPort;
        private readonly IConfirmPopupPort _confirmPopupPort;
        private readonly Action<MainMenuSectionId> _showSection;
        private readonly IMainMenuSettingsPort _settingsPort;

        public MainMenuHubController(
            IMainMenuSettingsPort settingsPort,
            IApplicationQuitPort applicationQuitPort,
            IConfirmPopupPort confirmPopupPort,
            Action<MainMenuSectionId> showSection)
        {
            _settingsPort = settingsPort ?? throw new ArgumentNullException(nameof(settingsPort));
            _applicationQuitPort = applicationQuitPort ?? throw new ArgumentNullException(nameof(applicationQuitPort));
            _confirmPopupPort = confirmPopupPort ?? throw new ArgumentNullException(nameof(confirmPopupPort));
            _showSection = showSection ?? throw new ArgumentNullException(nameof(showSection));
        }

        public void HandleCommand(MainMenuCommandIntent intent)
        {
            switch (intent.CommandKind)
            {
                case MainMenuCommandKind.OpenSettings:
                    _showSection(MainMenuSectionId.None);
                    _settingsPort.OpenSettings();
                    break;

                case MainMenuCommandKind.Quit:
                    RequestQuit();
                    break;
            }
        }

        public void HandleNavigation(MainMenuNavigationIntent intent)
        {
            if (intent.SectionId == MainMenuSectionId.None)
            {
                return;
            }

            _showSection(intent.SectionId);
        }

        private void RequestQuit()
        {
            _confirmPopupPort.Request(
                MainMenuLocalization.CreateConfirmationPayload(
                    MainMenuConfirmationKind.QuitGame),
                confirmed =>
                {
                    if (confirmed)
                    {
                        _applicationQuitPort.Quit();
                    }
                });
        }
    }
}
