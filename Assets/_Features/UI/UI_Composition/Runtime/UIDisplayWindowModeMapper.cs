using Game.Feature.UI.Application;
using SharedDisplayWindowMode = Game.Shared.Display.DisplayWindowMode;

namespace Game.Feature.UI.Composition
{
    internal static class UIDisplayWindowModeMapper
    {
        public static SharedDisplayWindowMode ToShared(DisplayWindowMode mode)
        {
            return mode == DisplayWindowMode.Windowed
                ? SharedDisplayWindowMode.Windowed
                : SharedDisplayWindowMode.FullScreenWindow;
        }

        public static DisplayWindowMode ToVisible(SharedDisplayWindowMode mode)
        {
            return mode == SharedDisplayWindowMode.Windowed
                ? DisplayWindowMode.Windowed
                : DisplayWindowMode.FullScreenWindow;
        }
    }
}
