namespace Game.Feature.UI.Composition
{
    internal interface ISceneTransitionOverlayContentView
    {
        void Bind(SceneTransitionOverlayViewModel model);
        void SetProgress(float progress01);
        void Show();
        void Hide();
        void ResetView();
    }
}
