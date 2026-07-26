using System;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public static class ObjectiveHudLocalization
    {
        public const string Table = "UI";

        public static class Keys
        {
            public const string Header = "ui.hud.objectives.title";
            public const string ReachExit = "ui.hud.objective.reach_exit";
            public const string ReachZone = "ui.hud.objective.reach_zone";
            public const string ActivateButton = "ui.hud.objective.activate_button";
            public const string ActivateMoonButton = "ui.hud.objective.activate_moon_button";
        }

        public static LocalizedTextDescriptor HeaderDescriptor =>
            new LocalizedTextDescriptor(
                Table,
                Keys.Header,
                LocalizedTextRole.Title,
                LocalizedTextWeight.Bold);

        public static bool TryCreateConditionDescriptor(
            GameplayObjectivePresentationKind presentationKind,
            int completedCount,
            int requiredCount,
            out LocalizedTextDescriptor descriptor)
        {
            var key = ResolveKey(presentationKind);
            if (string.IsNullOrEmpty(key))
            {
                descriptor = default;
                return false;
            }

            descriptor = new LocalizedTextDescriptor(
                Table,
                key,
                LocalizedTextRole.Body,
                LocalizedTextWeight.Regular,
                new object[]
                {
                    Math.Max(0, completedCount),
                    Math.Max(0, requiredCount),
                });
            return true;
        }

        private static string ResolveKey(GameplayObjectivePresentationKind presentationKind)
        {
            switch (presentationKind)
            {
                case GameplayObjectivePresentationKind.ReachExit:
                    return Keys.ReachExit;

                case GameplayObjectivePresentationKind.ReachZone:
                    return Keys.ReachZone;

                case GameplayObjectivePresentationKind.ActivateButton:
                    return Keys.ActivateButton;

                case GameplayObjectivePresentationKind.ActivateMoonButton:
                    return Keys.ActivateMoonButton;

                case GameplayObjectivePresentationKind.None:
                default:
                    return string.Empty;
            }
        }
    }
}
