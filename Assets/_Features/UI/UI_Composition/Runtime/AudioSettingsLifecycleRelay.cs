using Game.Feature.UI.Application;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    internal sealed class AudioSettingsLifecycleRelay : MonoBehaviour
    {
        private IAudioSettingsPort audioSettingsPort;

        public void Initialize(IAudioSettingsPort port)
        {
            audioSettingsPort = port;
        }

        public void FlushNow()
        {
            audioSettingsPort?.Flush();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                audioSettingsPort?.Flush();
            }
        }

        private void OnApplicationQuit()
        {
            audioSettingsPort?.Flush();
        }
    }
}
