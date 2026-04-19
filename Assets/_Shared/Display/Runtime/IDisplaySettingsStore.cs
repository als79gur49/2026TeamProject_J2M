namespace Game.Shared.Display
{
    public interface IDisplaySettingsStore
    {
        bool TryLoad(out DisplaySettingsSnapshot snapshot);

        void Save(DisplaySettingsSnapshot snapshot);
    }
}
