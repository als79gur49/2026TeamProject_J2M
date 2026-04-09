using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public struct StaticEntityPresentationCatalogEntry
    {
        public string PresentationId;
        public GameplayEntityView ViewPrefab;
    }

    [CreateAssetMenu(menuName = "Gameplay/Presentation/Static Entity Presentation Catalog")]
    public sealed class StaticEntityPresentationCatalog : ScriptableObject
    {
        [SerializeField] private StaticEntityPresentationCatalogEntry[] entries = Array.Empty<StaticEntityPresentationCatalogEntry>();

        public StaticEntityPresentationCatalogEntry[] Entries => entries ?? Array.Empty<StaticEntityPresentationCatalogEntry>();
    }
}
