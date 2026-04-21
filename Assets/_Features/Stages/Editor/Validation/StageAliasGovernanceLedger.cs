using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    [Serializable]
    public struct StageAliasGovernanceEntry
    {
        public string DeprecatedStageId;
        public StageId CurrentStageId;
        public string SourceKind;
        public string SourceAssetGuid;
        public string Owner;
        public string Reason;
        public string IntroducedBy;
        public string RemovalGate;
    }

    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Stage Alias Governance Ledger",
        fileName = "StageAliasGovernanceLedger")]
    public sealed class StageAliasGovernanceLedger : ScriptableObject
    {
        [SerializeField] private StageAliasGovernanceEntry[] entries = Array.Empty<StageAliasGovernanceEntry>();

        public IReadOnlyList<StageAliasGovernanceEntry> Entries => entries ?? Array.Empty<StageAliasGovernanceEntry>();

        public void SetEntries(StageAliasGovernanceEntry[] value)
        {
            entries = value ?? Array.Empty<StageAliasGovernanceEntry>();
        }
    }
}
