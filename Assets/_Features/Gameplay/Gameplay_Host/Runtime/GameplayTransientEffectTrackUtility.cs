using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplayTransientEffectTrackUtility
    {
        public static void SafeDestroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
