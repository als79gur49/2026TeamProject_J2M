using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Shared.Audio;

namespace Game.Feature.Stages
{
    public readonly struct StageBgmResolvedSlot
    {
        public StageBgmResolvedSlot(StageBgmSlotMode mode, BgmProfile profile)
        {
            Mode = mode;
            Profile = profile;
        }

        public StageBgmSlotMode Mode { get; }

        public BgmProfile Profile { get; }
    }

    public readonly struct StagePhaseBgmResolvedSlot
    {
        public StagePhaseBgmResolvedSlot(string phaseId, StageBgmResolvedSlot slot)
        {
            PhaseId = phaseId ?? string.Empty;
            Slot = slot;
        }

        public string PhaseId { get; }

        public StageBgmResolvedSlot Slot { get; }
    }

    public sealed class StageAudioResolvedData
    {
        public StageAudioResolvedData(
            StageBgmResolvedSlot gameplayBgm,
            StageBgmResolvedSlot previewBgm,
            StageBgmResolvedSlot clearResultBgm,
            StageBgmResolvedSlot failureResultBgm,
            IReadOnlyList<StagePhaseBgmResolvedSlot> phaseBgms)
        {
            GameplayBgm = gameplayBgm;
            PreviewBgm = previewBgm;
            ClearResultBgm = clearResultBgm;
            FailureResultBgm = failureResultBgm;
            PhaseBgms = CloneReadOnlyPhaseSlots(phaseBgms);
        }

        public StageBgmResolvedSlot GameplayBgm { get; }

        public StageBgmResolvedSlot PreviewBgm { get; }

        public StageBgmResolvedSlot ClearResultBgm { get; }

        public StageBgmResolvedSlot FailureResultBgm { get; }

        public IReadOnlyList<StagePhaseBgmResolvedSlot> PhaseBgms { get; }

        private static IReadOnlyList<StagePhaseBgmResolvedSlot> CloneReadOnlyPhaseSlots(
            IReadOnlyList<StagePhaseBgmResolvedSlot> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<StagePhaseBgmResolvedSlot>();
            }

            var copy = new StagePhaseBgmResolvedSlot[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return new ReadOnlyCollection<StagePhaseBgmResolvedSlot>(copy);
        }
    }
}
