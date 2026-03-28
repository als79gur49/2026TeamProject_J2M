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
        Flip = 7,
        Throw = Flip,
        Push = 9,
        BoxSlide = Push,
        Item = 10,
    }
}
