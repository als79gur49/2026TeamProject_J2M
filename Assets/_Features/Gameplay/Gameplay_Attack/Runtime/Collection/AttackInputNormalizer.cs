using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;

namespace Game.Feature.Gameplay.Attack.Collection
{
    internal sealed class AttackInputNormalizer
    {
        public void Normalize(
            IReadOnlyList<RawAttackIntent> rawAttackIntents,
            IReadOnlyList<ImpactReservation> impactReservations,
            List<AttackIntent> buffer)
        {
            if (rawAttackIntents == null)
            {
                throw new ArgumentNullException(nameof(rawAttackIntents));
            }

            if (impactReservations == null)
            {
                throw new ArgumentNullException(nameof(impactReservations));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            for (var i = 0; i < rawAttackIntents.Count; i++)
            {
                var rawIntent = rawAttackIntents[i];
                buffer.Add(AttackIntent.FromRawIntent(rawIntent));
            }

            for (var i = 0; i < impactReservations.Count; i++)
            {
                buffer.Add(AttackIntent.FromImpactReservation(impactReservations[i]));
            }

            buffer.Sort(AttackInputComparer.Instance);
            ValidateUniqueNormalizationKeys(buffer);
        }

        private static void ValidateUniqueNormalizationKeys(IReadOnlyList<AttackIntent> sortedInputs)
        {
            for (var i = 1; i < sortedInputs.Count; i++)
            {
                var previous = sortedInputs[i - 1];
                var current = sortedInputs[i];

                if (previous.SourceId != current.SourceId)
                {
                    continue;
                }

                if (previous.InputKind != current.InputKind)
                {
                    continue;
                }

                if (previous.LocalSequence != current.LocalSequence)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Duplicate attack input normalization key detected. Source={current.SourceId}, Kind={current.InputKind}, LocalSequence={current.LocalSequence}");
            }
        }
    }
}
