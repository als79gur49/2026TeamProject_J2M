using System;
using UnityEngine;

namespace Game.Shared.Display
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class DisplayRuntimeInstaller : MonoBehaviour
    {
        [SerializeField] private bool installOnAwake = true;

        private DisplaySettingsService displaySettingsService;
        private bool isInstalled;

        public IDisplaySettingsService DisplaySettingsService => displaySettingsService;

        public bool HasBootApplied => displaySettingsService?.HasBootApplied ?? false;

        private void Awake()
        {
            if (installOnAwake)
            {
                Install();
            }
        }

        public void Install()
        {
            if (isInstalled)
            {
                return;
            }

            displaySettingsService ??= new DisplaySettingsService(new PlayerPrefsDisplaySettingsStore());
            displaySettingsService.ApplyBootSettingsOnce();
            isInstalled = true;
        }

        internal void SetDisplaySettingsServiceForTesting(DisplaySettingsService service)
        {
            displaySettingsService = service ?? throw new ArgumentNullException(nameof(service));
            isInstalled = false;
        }
    }
}
