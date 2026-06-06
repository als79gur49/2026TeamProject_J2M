using System;

namespace Game.Feature.Stages
{
    public static class StageAudioAssembler
    {
        public static readonly StageAudioResolvedData EmptyResolvedData = new(
            new StageBgmResolvedSlot(StageBgmSlotMode.None, null));

        public static StageAudioResolvedData Resolve(StageAudioDefinition definition)
        {
            if (definition == null)
            {
                throw new InvalidOperationException("StageAudioDefinition reference cannot be null.");
            }

            definition.ValidateOrThrow();

            return new StageAudioResolvedData(
                ResolveSlot(definition.GameplayBgm));
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
    }
}
