using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct EnemyAnimationCueMetadata
    {
        public EnemyAnimationCueMetadata(
            EnemyAnimationCue cue,
            bool allowsTrigger,
            bool allowsState,
            bool supportsTiming,
            bool requiresSeparateSustainedStateWhenPrimaryTrigger = false)
        {
            Cue = cue;
            AllowsTrigger = allowsTrigger;
            AllowsState = allowsState;
            SupportsTiming = supportsTiming;
            RequiresSeparateSustainedStateWhenPrimaryTrigger =
                requiresSeparateSustainedStateWhenPrimaryTrigger;
        }

        public EnemyAnimationCue Cue { get; }

        public bool AllowsTrigger { get; }

        public bool AllowsState { get; }

        public bool SupportsTiming { get; }

        public bool RequiresResolvableState => AllowsState;

        public bool RequiresSeparateSustainedStateWhenPrimaryTrigger { get; }

        public bool Allows(EnemyAnimationDispatchMode mode)
        {
            return mode switch
            {
                EnemyAnimationDispatchMode.Trigger => AllowsTrigger,
                EnemyAnimationDispatchMode.State => AllowsState,
                _ => false,
            };
        }
    }

    internal static class EnemyAnimationCueCatalog
    {
        private static readonly EnemyAnimationCueMetadata[] MetadataEntries =
        {
            Both(EnemyAnimationCue.ActionWindup, supportsTiming: true),
            Trigger(EnemyAnimationCue.ActionExecute),
            Both(EnemyAnimationCue.ActionRecovery, supportsTiming: true),
            Both(EnemyAnimationCue.JumpWindup, supportsTiming: true),
            new EnemyAnimationCueMetadata(
                EnemyAnimationCue.JumpAirborne,
                allowsTrigger: true,
                allowsState: true,
                supportsTiming: true,
                requiresSeparateSustainedStateWhenPrimaryTrigger: true),
            State(EnemyAnimationCue.JumpLanding),
            Both(EnemyAnimationCue.ChargeWindup, supportsTiming: true),
            State(EnemyAnimationCue.ChargeActive),
            Both(EnemyAnimationCue.ChargeRecovery, supportsTiming: true),
            State(EnemyAnimationCue.GlideWindup, supportsTiming: true),
            State(EnemyAnimationCue.GlideActive),
            State(EnemyAnimationCue.GlideRecovery, supportsTiming: true),
            Both(EnemyAnimationCue.UtilityWindup, supportsTiming: true),
            Both(EnemyAnimationCue.UtilityRecovery, supportsTiming: true),
            Trigger(EnemyAnimationCue.Hit),
            Trigger(EnemyAnimationCue.Death),
        };

        private static readonly IReadOnlyList<EnemyAnimationCueMetadata> ReadOnlyMetadata =
            new ReadOnlyCollection<EnemyAnimationCueMetadata>(MetadataEntries);

        private static readonly IReadOnlyDictionary<EnemyAnimationCue, EnemyAnimationCueMetadata> ByCue =
            BuildLookup();

        internal static IReadOnlyList<EnemyAnimationCueMetadata> All => ReadOnlyMetadata;

        internal static bool TryGet(EnemyAnimationCue cue, out EnemyAnimationCueMetadata metadata)
        {
            return ByCue.TryGetValue(cue, out metadata);
        }

        internal static EnemyAnimationCueMetadata GetRequired(EnemyAnimationCue cue)
        {
            if (!TryGet(cue, out var metadata))
            {
                throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown or unsupported enemy animation cue.");
            }

            return metadata;
        }

        private static IReadOnlyDictionary<EnemyAnimationCue, EnemyAnimationCueMetadata> BuildLookup()
        {
            var byCue = new Dictionary<EnemyAnimationCue, EnemyAnimationCueMetadata>();
            for (var i = 0; i < MetadataEntries.Length; i++)
            {
                var metadata = MetadataEntries[i];
                if (!byCue.TryAdd(metadata.Cue, metadata))
                {
                    throw new InvalidOperationException($"Duplicate metadata for {metadata.Cue}.");
                }
            }

            return new ReadOnlyDictionary<EnemyAnimationCue, EnemyAnimationCueMetadata>(byCue);
        }

        private static EnemyAnimationCueMetadata Trigger(EnemyAnimationCue cue)
        {
            return new EnemyAnimationCueMetadata(cue, true, false, false);
        }

        private static EnemyAnimationCueMetadata State(EnemyAnimationCue cue, bool supportsTiming = false)
        {
            return new EnemyAnimationCueMetadata(cue, false, true, supportsTiming);
        }

        private static EnemyAnimationCueMetadata Both(EnemyAnimationCue cue, bool supportsTiming)
        {
            return new EnemyAnimationCueMetadata(cue, true, true, supportsTiming);
        }
    }
}
