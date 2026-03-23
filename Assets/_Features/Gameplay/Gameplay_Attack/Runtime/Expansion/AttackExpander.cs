using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using UnityEngine;

namespace Game.Feature.Gameplay.Attack.Expansion
{
    internal sealed class AttackExpander
    {
        private const int StageThreeDamageAmount = 1;

        public void Expand(
            WorldSnapshot snapshot,
            IReadOnlyList<AttackIntent> sortedInputs,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedInputs == null)
            {
                throw new ArgumentNullException(nameof(sortedInputs));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (rejectedReasons == null)
            {
                throw new ArgumentNullException(nameof(rejectedReasons));
            }

            buffer.Clear();
            rejectedReasons.Clear();

            for (var i = 0; i < sortedInputs.Count; i++)
            {
                var intent = sortedInputs[i];
                if (intent.IsSynthetic)
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Reason=SyntheticInputUnsupported|Kind={intent.InputKind}|LocalSequence={intent.LocalSequence}");
                    continue;
                }

                if (!snapshot.TryGetEntity(intent.SourceId, out var source))
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=MissingSource");
                    continue;
                }

                if (source.hp <= 0 || source.markedForDeath)
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=SourceNotAttackCapable|Hp={source.hp}|Marked={source.markedForDeath}");
                    continue;
                }

                if (intent.TargetId <= 0)
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=InvalidTargetId");
                    continue;
                }

                if (!snapshot.TryGetEntity(intent.TargetId, out var target))
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=MissingTarget");
                    continue;
                }

                if (!IsOrthogonallyAdjacent(source.position, target.position))
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=NotAdjacent|SourceCell=({source.position.x},{source.position.y})|TargetCell=({target.position.x},{target.position.y})");
                    continue;
                }

                if (!snapshot.CanBeTargetedForNewSelection(intent.TargetId))
                {
                    rejectedReasons.Add(
                        $"AttackRejected|Stage=Expand|I={intent.IntentId}|Source={intent.SourceId}|Target={intent.TargetId}|Reason=TargetNotSelectable");
                    continue;
                }

                var actionGroup = new ActionGroup(
                    intent.IntentId,
                    intent.SourceId,
                    intent.Priority,
                    ActionGroupKind.Attack);
                actionGroup.StateChanges.Add(
                    new StateChangeAction(
                        intent.SourceId,
                        EntityPhaseState.Acting,
                        0));
                actionGroup.Damages.Add(new DamageAction(intent.TargetId, StageThreeDamageAmount));
                // Stage3 keeps DestroyMark in the selected group chain; Commit decides whether it applies after accumulated damage.
                actionGroup.Destroys.Add(new DestroyAction(intent.TargetId));
                buffer.Add(actionGroup);
            }
        }

        private static bool IsOrthogonallyAdjacent(Vector2Int source, Vector2Int target)
        {
            var delta = source - target;
            return Math.Abs(delta.x) + Math.Abs(delta.y) == 1;
        }
    }
}
