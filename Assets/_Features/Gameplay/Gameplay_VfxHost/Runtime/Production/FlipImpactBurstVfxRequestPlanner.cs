using System;
using Game.Feature.Gameplay.Host;
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

            var flipImpactSignals = presentationData.FlipImpactSignals;
            for (var i = 0; i < flipImpactSignals.Count; i++)
            {
                var signal = flipImpactSignals[i];
                if (!FlipImpactContactVfxAnchorBuilder.TryBuild(
                        signal,
                        context.TimingProfile,
                        out var contactAnchor))
                {
                    continue;
                }

                AddFlipImpactBurstRequest(context, builder, contactAnchor);
            }
        }

        private static void AddFlipImpactBurstRequest(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            in FlipImpactContactVfxAnchor contactAnchor)
        {
            var correlationId = contactAnchor.SourceActionPlanId > 0
                ? contactAnchor.SourceActionPlanId
                : contactAnchor.BoxEntityId;

            builder.Add(
                new GameplayVfxRequest(
                    tickIndex: context.TickIndex,
                    sequenceId: correlationId,
                    presentationSeed: correlationId,
                    sourceEntityId: contactAnchor.BoxEntityId,
                    cueId: GameplayVfxCueId.From(BoxVfxCue.FlipImpactBurst),
                    anchor: VfxAnchor.ForCell(
                        contactAnchor.ImpactCell,
                        contactAnchor.Topology,
                        VfxAnchorSlot.CellFloor),
                    timing: VfxTimingKind.ImmediateOnTickPresentation,
                    isPersistent: false,
                    persistentKey: VfxPersistentKey.None));
        }
    }
}
