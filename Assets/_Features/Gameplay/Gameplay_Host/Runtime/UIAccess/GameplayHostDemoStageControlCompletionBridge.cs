using System;
using Game.Feature.DemoStageControl;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostDemoStageControlCompletionBridge : IDemoStageControlCompletionBridge
    {
        private readonly GameplayHostPresentationFeed _presentationFeed;

        public GameplayHostDemoStageControlCompletionBridge(GameplayHostPresentationFeed presentationFeed)
        {
            _presentationFeed = presentationFeed ?? throw new ArgumentNullException(nameof(presentationFeed));
        }

        public bool IsCompletionInProgress =>
            _presentationFeed.IsStageCompletionInProgress ||
            _presentationFeed.CurrentMinimalStageCompletion != null;

        public DemoStageControlResult ForceClearCurrentStage()
        {
            if (IsCompletionInProgress)
            {
                return DemoStageControlResult.Fail("Stage completion is already in progress or already completed.");
            }

            try
            {
                var readModel = _presentationFeed.ForceClearCurrentStage();
                if (readModel == null)
                {
                    return DemoStageControlResult.Fail(
                        "Forced clear was rejected by the active terminal-session authority.");
                }

                return DemoStageControlResult.Ok(
                    readModel.StageId.IsValid
                        ? $"Forced clear committed for '{readModel.StageId.Value}'."
                        : "Forced clear committed.");
            }
            catch (Exception exception)
            {
                return DemoStageControlResult.Fail(exception.Message);
            }
        }
    }
}
