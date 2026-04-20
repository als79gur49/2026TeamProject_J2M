using System;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    internal sealed class DisplaySettingsLifecycleRelay : MonoBehaviour
    {
        public event Action ResyncRequested;

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ResyncRequested?.Invoke();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                ResyncRequested?.Invoke();
            }
        }
    }
}
