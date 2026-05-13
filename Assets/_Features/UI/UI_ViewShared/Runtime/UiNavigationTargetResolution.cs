namespace Game.Feature.UI.ViewShared
{
    public readonly struct UiNavigationTargetResolution
    {
        public UiNavigationTargetResolution(IUiNavigationTarget target, bool blocksLowerLayers)
        {
            Target = target;
            BlocksLowerLayers = blocksLowerLayers;
        }

        public IUiNavigationTarget Target { get; }

        public bool BlocksLowerLayers { get; }

        public static UiNavigationTargetResolution Open(IUiNavigationTarget target)
        {
            return new UiNavigationTargetResolution(target, blocksLowerLayers: false);
        }

        public static UiNavigationTargetResolution Blocking(IUiNavigationTarget target)
        {
            return new UiNavigationTargetResolution(target, blocksLowerLayers: true);
        }
    }
}
