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
        private readonly IParticipantResetPort _participantReset;
        private readonly Func<bool> _interactionBlocked;
        private bool _confirmationPending;

        public MainMenuHubController(
            IMainMenuSettingsPort settingsPort,
            IApplicationQuitPort applicationQuitPort,
            IConfirmPopupPort confirmPopupPort,
            Action<MainMenuSectionId> showSection,
            IParticipantResetPort participantReset = null, Func<bool> interactionBlocked = null)
        {
            _participantReset = participantReset;
            _interactionBlocked = interactionBlocked ?? (() => false);
            _settingsPort = settingsPort ?? throw new ArgumentNullException(nameof(settingsPort));
            _applicationQuitPort = applicationQuitPort ?? throw new ArgumentNullException(nameof(applicationQuitPort));
            _confirmPopupPort = confirmPopupPort ?? throw new ArgumentNullException(nameof(confirmPopupPort));
            _showSection = showSection ?? throw new ArgumentNullException(nameof(showSection));
        }

        public void HandleCommand(MainMenuCommandIntent intent)
        {
            if (_interactionBlocked() || _participantReset?.BlocksMenu == true) return;
            switch (intent.CommandKind)
            {
                case MainMenuCommandKind.OpenSettings:
                    _showSection(MainMenuSectionId.None);
                    _settingsPort.OpenSettings();
                    break;

                case MainMenuCommandKind.PrepareParticipant:
                    if (_confirmationPending || _participantReset?.CanRequest != true) return;
                    _confirmationPending = true;
                    _confirmPopupPort.Request(MainMenuLocalization.CreateConfirmationPayload(
                        MainMenuConfirmationKind.PrepareParticipant), confirmed =>
                    {
                        _confirmationPending = false;
                        if (confirmed && !_interactionBlocked() && _participantReset.CanRequest)
                            _participantReset.RequestReset();
                    });
                    break;

                case MainMenuCommandKind.Quit:
                    RequestQuit();
                    break;
            }
        }

        public void HandleNavigation(MainMenuNavigationIntent intent)
        {
            if (_interactionBlocked() || _participantReset?.BlocksMenu == true ||
                intent.SectionId == MainMenuSectionId.None)
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
