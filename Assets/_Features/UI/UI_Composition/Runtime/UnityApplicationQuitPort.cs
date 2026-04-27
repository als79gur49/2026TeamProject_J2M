using Game.Feature.UI.Application;

namespace Game.Feature.UI.Composition
{
    public sealed class UnityApplicationQuitPort : IApplicationQuitPort
    {
        public void Quit()
        {
            UnityEngine.Application.Quit();
        }
    }
}
