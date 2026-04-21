using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    [Serializable]
    public struct StageCatalogKnownWarningEntry
    {
        public string IssueCode;
        public string AssetGuid;
        public string ExpectedAssetPath;
        public string ExpectedAssetName;
        public string ExpectedStageId;
        public string Owner;
        public string Reason;
        public string RemovalGate;
    }

    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Stage Catalog Known Warning Ledger",
        fileName = "StageCatalogKnownWarningLedger")]
    public sealed class StageCatalogKnownWarningLedger : ScriptableObject
    {
        [SerializeField] private StageCatalogKnownWarningEntry[] entries = Array.Empty<StageCatalogKnownWarningEntry>();

        public IReadOnlyList<StageCatalogKnownWarningEntry> Entries => entries ?? Array.Empty<StageCatalogKnownWarningEntry>();

        public void SetEntries(StageCatalogKnownWarningEntry[] value)
        {
            entries = value ?? Array.Empty<StageCatalogKnownWarningEntry>();
        }
    }
}
