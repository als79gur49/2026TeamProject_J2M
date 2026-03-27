namespace Game.Feature.Gameplay.Model.Groups
{
    public enum ActionGroupKind
    {
        None = 0,
        Move = 1,
        Stop = 2,
        Attack = 3,
        Cleanup = 4,
        ProjectileImpact = 5,
        Throw = 7,
        Flip = Throw,
        InteractLootDestroy = 8,
        BoxSlide = 9,
        Push = BoxSlide,
        Item = 10,
    }
}
