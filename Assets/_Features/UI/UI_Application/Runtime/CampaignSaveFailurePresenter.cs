using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public enum CampaignSaveFailureChoice { Menu, Quit, Dismissed }

    public interface ICampaignSaveFailureDialogPort
    {
        void Request(ConfirmPopupPayload payload, Action<CampaignSaveFailureChoice> completion);
    }

    public sealed class CampaignSaveFailurePresenter : IDisposable
    {
        private readonly IGameplayCampaignFailureSource _source;
        private readonly ICampaignSaveFailureDialogPort _popup;
        private readonly Action _returnToMenu;
        private readonly IApplicationQuitPort _quit;
        private bool _disposed;
        private bool _showing;
        private bool _leaving;

        public CampaignSaveFailurePresenter(IGameplayCampaignFailureSource source,
            ICampaignSaveFailureDialogPort popup, Action returnToMenu, IApplicationQuitPort quit)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _popup = popup ?? throw new ArgumentNullException(nameof(popup));
            _returnToMenu = returnToMenu ?? throw new ArgumentNullException(nameof(returnToMenu));
            _quit = quit ?? throw new ArgumentNullException(nameof(quit));
            _source.CampaignRunFailed += Show;
            if (_source.HasCampaignRunFailure) Show();
        }

        private void Show()
        {
            if (_disposed || _showing || _leaving) return;
            _showing = true;
            var handled = false;
            try
            {
                _popup.Request(new ConfirmPopupPayload(
                    new LocalizedTextDescriptor("UI", "ui.campaign.save_error.title", LocalizedTextRole.Title),
                    new LocalizedTextDescriptor("UI", "ui.campaign.save_error.detail"),
                    new LocalizedTextDescriptor("UI", "ui.main_menu.quit", LocalizedTextRole.Button),
                    TerminalResultTextDescriptors.MainMenu, true)
                { ConsumeBack = true }, choice =>
                {
                    if (_disposed || handled) return;
                    handled = true;
                    _showing = false;
                    if (choice == CampaignSaveFailureChoice.Dismissed) return;
                    _leaving = true;
                    try
                    {
                        if (choice == CampaignSaveFailureChoice.Menu) _returnToMenu();
                        else _quit.Quit();
                    }
                    catch
                    {
                        // A rejected route changes neither the saved result nor the abandoned run.
                        _leaving = false;
                        Show();
                    }
                });
            }
            catch { _showing = false; throw; }
        }

        public void Dispose()
        {
            _disposed = true;
            _source.CampaignRunFailed -= Show;
        }
    }
}
