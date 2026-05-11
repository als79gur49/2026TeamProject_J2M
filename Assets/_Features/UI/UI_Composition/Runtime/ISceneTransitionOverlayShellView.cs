namespace Game.Feature.UI.Composition
{
    internal interface ISceneTransitionOverlayShellView
    {
        void ShowBlockerOnly(bool blockInput);
        ISceneTransitionOverlayContentView MountContent(SceneTransitionOverlayContentView contentPrefab);
        void ShowContent(SceneTransitionOverlayViewModel model, ISceneTransitionOverlayContentView content);
        void SetProgress(float progress01);
        void HideVisual();
        void HideAll();
    }
}
