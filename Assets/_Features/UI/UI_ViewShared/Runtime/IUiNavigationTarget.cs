namespace Game.Feature.UI.ViewShared
{
    public interface IUiNavigationTarget
    {
        bool CanHandleUiNavigation { get; }

        bool HandleNavigate(UiNavigationCommand command);

        bool HandleSubmit();

        bool HandleCancel();

        void OnNavigationFocusGained();

        void OnNavigationFocusLost();
    }
}
