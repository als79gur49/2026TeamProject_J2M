using UnityEngine.InputSystem.UI;

namespace Game.Feature.UI.Composition
{
    internal static class UiEventSystemNavigationActionUtility
    {
        internal static void DisableNavigationActions(InputSystemUIInputModule module)
        {
            if (module == null)
            {
                return;
            }

            module.move = null;
            module.submit = null;
            module.cancel = null;
        }
    }
}
