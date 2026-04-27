namespace Game.Feature.UI.Application
{
    public interface IMainMenuSettingsPort
    {
        void OpenSettings();
    }

    public sealed class NoOpMainMenuSettingsPort : IMainMenuSettingsPort
    {
        public static readonly NoOpMainMenuSettingsPort Instance = new();

        private NoOpMainMenuSettingsPort()
        {
        }

        public void OpenSettings()
        {
        }
    }

    public interface IApplicationQuitPort
    {
        void Quit();
    }
}
