namespace Game.Feature.Gameplay.Host
{
    public enum EnemyPresentationEffectInactivePolicy
    {
        IgnoreInactiveSemantic = 0,
        StopOnInactiveResumeOnNormal = 1,
        StopOnInactiveDoNotResume = 2,
        HideRendererOnly = 3,
    }
}
