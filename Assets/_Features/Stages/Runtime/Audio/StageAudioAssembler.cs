using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public static class StageAudioAssembler
    {
        public static readonly StageAudioResolvedData EmptyResolvedData = new(
            new StageBgmResolvedSlot(StageBgmSlotMode.None, null),
            new StageBgmResolvedSlot(StageBgmSlotMode.None, null),
            new StageBgmResolvedSlot(StageBgmSlotMode.None, null),
            new StageBgmResolvedSlot(StageBgmSlotMode.None, null),
            Array.Empty<StagePhaseBgmResolvedSlot>());

        public static StageAudioResolvedData Resolve(StageAudioDefinition definition)
        {
            if (definition == null)
            {
                throw new InvalidOperationException("StageAudioDefinition reference cannot be null.");
            }

            definition.ValidateOrThrow();

            return new StageAudioResolvedData(
                ResolveSlot(definition.GameplayBgm),
                ResolveSlot(definition.PreviewBgm),
                ResolveSlot(definition.ClearResultBgm),
                ResolveSlot(definition.FailureResultBgm),
                ResolvePhaseSlots(definition.PhaseBgms));
        }

        private static StageBgmResolvedSlot ResolveSlot(StageBgmSlot slot)
        {
            if (slot == null)
            {
                return new StageBgmResolvedSlot(StageBgmSlotMode.None, null);
            }

            return slot.Mode == StageBgmSlotMode.Profile
                ? new StageBgmResolvedSlot(StageBgmSlotMode.Profile, slot.Profile)
                : new StageBgmResolvedSlot(StageBgmSlotMode.None, null);
        }

        private static IReadOnlyList<StagePhaseBgmResolvedSlot> ResolvePhaseSlots(
            IReadOnlyList<StagePhaseBgmSlot> phaseBgms)
        {
            if (phaseBgms == null || phaseBgms.Count == 0)
            {
                return Array.Empty<StagePhaseBgmResolvedSlot>();
            }

            var resolved = new StagePhaseBgmResolvedSlot[phaseBgms.Count];
            for (var i = 0; i < phaseBgms.Count; i++)
            {
                var phase = phaseBgms[i];
                resolved[i] = new StagePhaseBgmResolvedSlot(phase.PhaseId, ResolveSlot(phase.Slot));
            }

            return resolved;
        }
    }
}
