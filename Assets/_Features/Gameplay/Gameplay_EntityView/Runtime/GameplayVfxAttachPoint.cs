using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayVfxAttachPoint : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => id;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (id != null)
            {
                id = id.Trim();
            }
        }
#endif
    }
}
