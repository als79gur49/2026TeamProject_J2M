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
        private FullscreenCursorConfinementPolicy fullscreenCursorConfinementPolicy;
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

        private void Update()
        {
            if (isInstalled)
            {
                fullscreenCursorConfinementPolicy?.Reconcile();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (isInstalled)
            {
                fullscreenCursorConfinementPolicy?.Reconcile(hasFocus);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!isInstalled)
            {
                return;
            }

            if (pauseStatus)
            {
                fullscreenCursorConfinementPolicy?.Reconcile(false);
                return;
            }

            fullscreenCursorConfinementPolicy?.Reconcile();
        }

        public void Install()
        {
            if (isInstalled)
            {
                return;
            }

            displaySettingsService ??= new DisplaySettingsService(new PlayerPrefsDisplaySettingsStore());
            fullscreenCursorConfinementPolicy ??= new FullscreenCursorConfinementPolicy(
                new UnityCursorConfinementRuntimeGateway());
            displaySettingsService.ApplyBootSettingsOnce();
            isInstalled = true;
            fullscreenCursorConfinementPolicy.Reconcile();
        }

        internal void SetDisplaySettingsServiceForTesting(DisplaySettingsService service)
        {
            displaySettingsService = service ?? throw new ArgumentNullException(nameof(service));
            isInstalled = false;
        }

        internal void SetFullscreenCursorConfinementPolicyForTesting(
            FullscreenCursorConfinementPolicy policy)
        {
            fullscreenCursorConfinementPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            isInstalled = false;
        }
    }
}
