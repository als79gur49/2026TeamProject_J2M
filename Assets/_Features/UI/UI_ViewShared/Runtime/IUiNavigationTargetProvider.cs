namespace Game.Feature.UI.ViewShared
{
    public interface IUiNavigationTargetProvider
    {
        bool TryGetNavigationTarget(out IUiNavigationTarget target);
    }
}
