using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        fileName = "BoardPresentationProfile",
        menuName = "Game/Stages/Board Presentation Profile")]
    public sealed class BoardPresentationProfile : ScriptableObject
    {
        [SerializeField] private GameObject boardRootPrefab;
        [SerializeField] private GameObject activeFaceCoverPrefab;
        [SerializeField] private BoardTileStyleCatalog defaultBoardTileStyleCatalog;

        public GameObject BoardRootPrefab => boardRootPrefab;

        public GameObject ActiveFaceCoverPrefab => activeFaceCoverPrefab;

        public BoardTileStyleCatalog DefaultBoardTileStyleCatalog => defaultBoardTileStyleCatalog;
    }
}
