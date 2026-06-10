using System;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Flow
{
    internal readonly struct PauseReturnContext
    {
        public PauseReturnContext(
            bool shouldRestorePausePopupAfterBack,
            bool screenHandledBack,
            ScreenId currentScreenId)
        {
            ShouldRestorePausePopupAfterBack = shouldRestorePausePopupAfterBack;
            ScreenHandledBack = screenHandledBack;
            CurrentScreenId = currentScreenId;
        }

        public bool ShouldRestorePausePopupAfterBack { get; }

        public bool ScreenHandledBack { get; }

        public ScreenId CurrentScreenId { get; }
    }

    internal readonly struct PauseReturnDecision
    {
        public PauseReturnDecision(
            bool shouldReopenPausePopup,
            bool shouldReturnToGameplayScreen,
            bool shouldDismissSettingsScreen,
            bool shouldClearPauseReturnMode)
        {
            ShouldReopenPausePopup = shouldReopenPausePopup;
            ShouldReturnToGameplayScreen = shouldReturnToGameplayScreen;
            ShouldDismissSettingsScreen = shouldDismissSettingsScreen;
            ShouldClearPauseReturnMode = shouldClearPauseReturnMode;
        }

        public bool ShouldReopenPausePopup { get; }

        public bool ShouldReturnToGameplayScreen { get; }

        public bool ShouldDismissSettingsScreen { get; }

        public bool ShouldClearPauseReturnMode { get; }
    }

    internal static class PauseReturnPolicy
    {
        public static PauseReturnDecision Decide(PauseReturnContext context)
        {
            var restoredToGameplay =
                context.ScreenHandledBack &&
                context.CurrentScreenId == ScreenId.Gameplay;
            var shouldReopenPausePopup =
                context.ShouldRestorePausePopupAfterBack &&
                restoredToGameplay;

            return new PauseReturnDecision(
                shouldReopenPausePopup,
                restoredToGameplay,
                restoredToGameplay,
                shouldReopenPausePopup);
        }
    }
}
