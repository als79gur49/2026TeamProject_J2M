namespace Game.Shared.Input
{
    public static class GameplayInputActionPaths
    {
        public const string PlayerActionMap = "Player";
        public const string UiActionMap = "UI";

        public const string MoveAction = "Move";
        public const string PushAction = "Push";
        public const string FlipAction = "Flip";
        public const string NavigateAction = "Navigate";

        public const string PlayerMove = PlayerActionMap + "/" + MoveAction;
        public const string PlayerPush = PlayerActionMap + "/" + PushAction;
        public const string PlayerFlip = PlayerActionMap + "/" + FlipAction;
        public const string UiNavigate = UiActionMap + "/" + NavigateAction;

        public static readonly string[] RequiredGameplayActions =
        {
            PlayerMove,
            PlayerPush,
            PlayerFlip,
        };

        public static readonly string[] RequiredSettingsActions =
        {
            PlayerMove,
            PlayerPush,
            PlayerFlip,
            UiNavigate,
        };
    }
}
