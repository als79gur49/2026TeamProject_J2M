using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Composition
{
    internal sealed class SingleUiNavigationTargetProvider : IUiNavigationTargetProvider
    {
        private readonly IUiNavigationTarget _target;

        public SingleUiNavigationTargetProvider(IUiNavigationTarget target)
        {
            _target = target;
        }

        public bool TryGetNavigationTarget(out IUiNavigationTarget target)
        {
            target = _target;
            return target != null;
        }
    }
}
