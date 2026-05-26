using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Background Wall Surface Tint Profile",
        fileName = "BackgroundWallSurfaceTintProfile")]
    public sealed class BackgroundWallSurfaceTintProfile : ScriptableObject
    {
        [SerializeField] private Color floorBaseColor = Color.white;
        [SerializeField] private Color frontBaseColor = Color.white;
        [SerializeField] private Color ceilingBaseColor = Color.white;
        [SerializeField] private Color backBaseColor = Color.white;
        [SerializeField] private Color floorEmissionColor = Color.black;
        [SerializeField] private Color frontEmissionColor = Color.black;
        [SerializeField] private Color ceilingEmissionColor = Color.black;
        [SerializeField] private Color backEmissionColor = Color.black;
        [SerializeField] private bool preserveMaterialAlpha = true;

        public Color FloorBaseColor => floorBaseColor;

        public Color FrontBaseColor => frontBaseColor;

        public Color CeilingBaseColor => ceilingBaseColor;

        public Color BackBaseColor => backBaseColor;

        public Color FloorEmissionColor => floorEmissionColor;

        public Color FrontEmissionColor => frontEmissionColor;

        public Color CeilingEmissionColor => ceilingEmissionColor;

        public Color BackEmissionColor => backEmissionColor;

        public bool PreserveMaterialAlpha => preserveMaterialAlpha;

        public bool TryGetBaseColor(FaceId face, out Color color)
        {
            switch (face)
            {
                case FaceId.Floor:
                    color = floorBaseColor;
                    return true;
                case FaceId.Front:
                    color = frontBaseColor;
                    return true;
                case FaceId.Ceiling:
                    color = ceilingBaseColor;
                    return true;
                case FaceId.Back:
                    color = backBaseColor;
                    return true;
                default:
                    color = default;
                    return false;
            }
        }

        public bool TryGetEmissionColor(FaceId face, out Color color)
        {
            switch (face)
            {
                case FaceId.Floor:
                    color = floorEmissionColor;
                    return true;
                case FaceId.Front:
                    color = frontEmissionColor;
                    return true;
                case FaceId.Ceiling:
                    color = ceilingEmissionColor;
                    return true;
                case FaceId.Back:
                    color = backEmissionColor;
                    return true;
                default:
                    color = default;
                    return false;
            }
        }
    }
}
