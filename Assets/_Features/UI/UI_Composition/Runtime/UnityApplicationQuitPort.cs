using Game.Feature.UI.Application;

namespace Game.Feature.UI.Composition
{
    public sealed class UnityApplicationQuitPort : IApplicationQuitPort
    {
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
