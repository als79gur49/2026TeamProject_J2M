namespace Game.Feature.UI.Composition
{
    internal interface ISceneTransitionOverlayView
    {
        void ShowBlockerOnly(bool blockInput);

        void ShowOverlay(SceneTransitionOverlayViewModel model);

        void SetProgress(float progress01);

        void HideVisual();

        void HideAll();
    }
}
