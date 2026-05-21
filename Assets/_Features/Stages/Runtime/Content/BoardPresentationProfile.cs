using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        fileName = "BoardPresentationProfile",
        menuName = "Game/Stages/Board Presentation Profile")]
    public sealed class BoardPresentationProfile : ScriptableObject
    {
        [SerializeField] private GameObject boardRootPrefab;
        [SerializeField] private BoardTileStyleCatalog defaultBoardTileStyleCatalog;
        [SerializeField] private BoardTileOverlayCatalog defaultBoardTileOverlayCatalog;

        public GameObject BoardRootPrefab => boardRootPrefab;

        public BoardTileStyleCatalog DefaultBoardTileStyleCatalog => defaultBoardTileStyleCatalog;

        public BoardTileOverlayCatalog DefaultBoardTileOverlayCatalog => defaultBoardTileOverlayCatalog;
    }
}
