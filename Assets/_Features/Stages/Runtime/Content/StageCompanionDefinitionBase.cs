using UnityEngine;

namespace Game.Feature.Stages
{
    public abstract class StageCompanionDefinitionBase : ScriptableObject
    {
        [SerializeField, HideInInspector] private StageContentEntry ownerEntry;
        [SerializeField, HideInInspector] private string ownerEntryGuid = string.Empty;

        public StageContentEntry OwnerEntry => ownerEntry;

        public string OwnerEntryGuid => ownerEntryGuid ?? string.Empty;

        public void SetOwnerMetadata(StageContentEntry owner, string guid)
        {
            ownerEntry = owner;
            ownerEntryGuid = guid ?? string.Empty;
        }
    }
}
