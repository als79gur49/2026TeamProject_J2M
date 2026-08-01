using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    internal interface ISceneTransitionOverlayShellView
    {
        bool IsOpaqueHandoffReady { get; }
        bool HasRenderedOpaqueFrame { get; }
        void ShowBlockerOnly(bool blockInput);
        void RequestOpaqueTakeover(UnityEngine.Color color);
        void RequestStyledCoverTakeover(
            UnityEngine.Color color,
            float fadeDuration,
            TerminalIrisEasing easing);
        void TickStyledCoverTakeover(float unscaledDeltaTime);
        bool IsStyledCoverFadeComplete { get; }
        void AcknowledgeOpaqueHandoffReady();
        void SetPersistentCoverOpacity(float opacity);
        ISceneTransitionOverlayContentView MountContent(SceneTransitionOverlayContentView contentPrefab);
        void ShowContent(SceneTransitionOverlayModel model, ISceneTransitionOverlayContentView content);
        void SetProgress(float progress01);
        void HideVisual();
        void HideAll();
    }
}
