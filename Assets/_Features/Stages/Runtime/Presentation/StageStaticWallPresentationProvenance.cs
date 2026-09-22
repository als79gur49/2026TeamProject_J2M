using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Stages
{
    internal enum StageStaticWallProvenanceSourceKind
    {
        StageAuthoredStaticWall = 1,
    }

    internal readonly struct StageStaticWallProvenanceEntry
    {
        internal StageStaticWallProvenanceEntry(
            int entityId,
            StageStaticWallProvenanceSourceKind sourceKind,
            EntityState initialWall,
            string signature)
        {
            EntityId = entityId;
            SourceKind = sourceKind;
            InitialWall = initialWall;
            Signature = signature ?? throw new ArgumentNullException(nameof(signature));
        }

        internal int EntityId { get; }
        internal StageStaticWallProvenanceSourceKind SourceKind { get; }
        internal EntityState InitialWall { get; }
        internal string Signature { get; }
    }

    internal abstract class StageStaticWallPresentationProvenance
    {
        private protected StageStaticWallPresentationProvenance()
        {
        }

        internal static StageStaticWallPresentationProvenance Empty { get; } = new EmptyProvenance();

        internal abstract int Count { get; }

        internal abstract string StaticRevision { get; }

        internal abstract IReadOnlyList<int> EntityIds { get; }

        internal abstract bool TryGetEntry(int entityId, out StageStaticWallProvenanceEntry entry);

        private sealed class EmptyProvenance : StageStaticWallPresentationProvenance
        {
            internal override int Count => 0;

            internal override string StaticRevision => StageStaticWallProvenanceSignature.EmptyRevision;

            internal override IReadOnlyList<int> EntityIds => System.Array.Empty<int>();

            internal override bool TryGetEntry(int entityId, out StageStaticWallProvenanceEntry entry)
            {
                entry = default;
                return false;
            }
        }
    }

    internal static class StageStaticWallProvenanceSignature
    {
        internal static string EmptyRevision { get; } = ComputeSha256("stage-static-wall-provenance-v2\n");

        internal static string BuildEntry(StageSpawnKind sourceKind, EntityState entity)
        {
            var builder = new StringBuilder();
            builder.Append("sourceKind=").Append(((int)sourceKind).ToString(CultureInfo.InvariantCulture)).Append('|');
            AppendEntityState(builder, entity);
            return ComputeSha256(builder.ToString());
        }

        internal static string BuildRevision(IReadOnlyList<int> entityIds, IReadOnlyDictionary<int, string> signatures)
        {
            var builder = new StringBuilder("stage-static-wall-provenance-v2\n");
            for (var i = 0; i < entityIds.Count; i++)
            {
                var entityId = entityIds[i];
                builder.Append(entityId.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(signatures[entityId]).Append('\n');
            }

            return ComputeSha256(builder.ToString());
        }

        internal static void AppendEntityState(StringBuilder builder, EntityState entity)
        {
            AppendInvariant(builder, entity.entityId).Append('|');
            AppendInvariant(builder, (int)entity.position.face).Append(',');
            AppendInvariant(builder, entity.position.x).Append(',');
            AppendInvariant(builder, entity.position.y).Append('|');
            AppendInvariant(builder, entity.hp).Append('|');
            AppendInvariant(builder, entity.maxHp).Append('|');
            AppendInvariant(builder, entity.teamId).Append('|');
            AppendInvariant(builder, (int)entity.type).Append('|');
            AppendInvariant(builder, (int)entity.unitRole).Append('|');
            AppendInvariant(builder, (int)entity.unitMobilityKind).Append('|');
            AppendInvariant(builder, (int)entity.state).Append('|');
            AppendInvariant(builder, entity.stateTimer).Append('|');
            AppendInvariant(builder, (int)entity.facing).Append('|');
            AppendInvariant(builder, (int)entity.boardPresence).Append('|');
            AppendInvariant(builder, entity.markedForDeath ? 1 : 0).Append('|');
            AppendInvariant(builder, entity.spawnTick).Append('|');
            AppendInvariant(builder, (int)entity.boxCapabilities).Append('|');
            AppendInvariant(builder, (int)entity.boxArchetype).Append('|');
            AppendInvariant(builder, (int)entity.gravityFieldPhase).Append('|');
            AppendInvariant(builder, entity.gravityFieldTimerTicks).Append('|');
            AppendInvariant(builder, entity.kineticInstigatorEntityId).Append('|');
            AppendInvariant(builder, entity.kineticInstigatorTeamId).Append('|');
            AppendInvariant(builder, (int)entity.aiMode).Append('|');
            AppendInvariant(builder, entity.aiStateTimer).Append('|');
            AppendInvariant(builder, entity.enemyLocomotionCooldownTicks).Append('|');
            AppendInvariant(builder, entity.enemyAttackCooldownTicks).Append('|');
            AppendInvariant(builder, entity.enemyAttackCooldownTotalTicks);
        }

        private static StringBuilder AppendInvariant(StringBuilder builder, int value)
        {
            return builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        internal static bool SemanticallyEquals(EntityState left, EntityState right)
        {
            return left.entityId == right.entityId &&
                   left.position.Equals(right.position) &&
                   left.hp == right.hp &&
                   left.maxHp == right.maxHp &&
                   left.teamId == right.teamId &&
                   left.type == right.type &&
                   left.unitRole == right.unitRole &&
                   left.unitMobilityKind == right.unitMobilityKind &&
                   left.state == right.state &&
                   left.stateTimer == right.stateTimer &&
                   left.facing == right.facing &&
                   left.boardPresence == right.boardPresence &&
                   left.markedForDeath == right.markedForDeath &&
                   left.spawnTick == right.spawnTick &&
                   left.boxCapabilities == right.boxCapabilities &&
                   left.boxArchetype == right.boxArchetype &&
                   left.gravityFieldPhase == right.gravityFieldPhase &&
                   left.gravityFieldTimerTicks == right.gravityFieldTimerTicks &&
                   left.kineticInstigatorEntityId == right.kineticInstigatorEntityId &&
                   left.kineticInstigatorTeamId == right.kineticInstigatorTeamId &&
                   left.aiMode == right.aiMode &&
                   left.aiStateTimer == right.aiStateTimer &&
                   left.enemyLocomotionCooldownTicks == right.enemyLocomotionCooldownTicks &&
                   left.enemyAttackCooldownTicks == right.enemyAttackCooldownTicks &&
                   left.enemyAttackCooldownTotalTicks == right.enemyAttackCooldownTotalTicks;
        }

        internal static string CanonicalizeEntityState(EntityState entity)
        {
            var builder = new StringBuilder();
            AppendEntityState(builder, entity);
            return builder.ToString();
        }

        internal static string ComputeSha256(string value)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            var builder = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
