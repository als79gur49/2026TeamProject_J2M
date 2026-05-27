using System;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public sealed class FlipImpactBurstVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Box;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var presentationData = context.PresentationData;
            if (presentationData == null)
            {
                return;
            }

            var flipFloorImpactSignals = presentationData.FlipFloorImpactSignals;
            for (var i = 0; i < flipFloorImpactSignals.Count; i++)
            {
                AddFlipImpactBurstRequest(context, builder, flipFloorImpactSignals[i]);
            }
        }

        private static void AddFlipImpactBurstRequest(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            in FlipFloorImpactPresentationSignal signal)
        {
            if (signal.BoxEntityId <= 0)
            {
                return;
            }

            var correlationId = signal.SourceActionPlanId > 0
                ? signal.SourceActionPlanId
                : signal.BoxEntityId;
            var delaySeconds = signal.TimingMode == GameplayPresentationTimingMode.DueContactImmediate
                ? 0f
                : context.TimingProfile.FlipMotionDurationSeconds * signal.VisualContactNormalizedTime;

            builder.Add(
                new GameplayVfxRequest(
                    tickIndex: context.TickIndex,
                    sequenceId: correlationId,
                    presentationSeed: correlationId,
                    sourceEntityId: signal.BoxEntityId,
                    cueId: GameplayVfxCueId.From(BoxVfxCue.FlipImpactBurst),
                    anchor: VfxAnchor.ForCell(
                        signal.ContactCell,
                        signal.Topology,
                        VfxAnchorSlot.CellFloor),
                    timing: delaySeconds > 0f
                        ? VfxTimingKind.Delayed
                        : VfxTimingKind.ImmediateOnTickPresentation,
                    isPersistent: false,
                    persistentKey: VfxPersistentKey.None,
                    delaySeconds: delaySeconds));
        }
    }
}
