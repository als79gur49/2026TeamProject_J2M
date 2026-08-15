using System;
using UnityEngine;

namespace Game.Shared.Display
{
    internal interface ICursorConfinementRuntimeGateway
    {
        bool SupportsConfinement { get; }

        bool IsApplicationFocused { get; }

        DisplayWindowMode ReadCurrentWindowMode();

        CursorLockMode ReadCurrentLockState();

        void SetLockState(CursorLockMode lockState);
    }

    internal sealed class FullscreenCursorConfinementPolicy
    {
        private readonly ICursorConfinementRuntimeGateway runtimeGateway;

        public FullscreenCursorConfinementPolicy(ICursorConfinementRuntimeGateway runtimeGateway)
        {
            this.runtimeGateway = runtimeGateway ?? throw new ArgumentNullException(nameof(runtimeGateway));
        }

        public void Reconcile()
        {
            Reconcile(runtimeGateway.IsApplicationFocused);
        }

        public void Reconcile(bool isApplicationFocused)
        {
            if (!runtimeGateway.SupportsConfinement)
            {
                return;
            }

            var desiredLockState = isApplicationFocused &&
                                   runtimeGateway.ReadCurrentWindowMode() == DisplayWindowMode.FullScreenWindow
                ? CursorLockMode.Confined
                : CursorLockMode.None;
            if (runtimeGateway.ReadCurrentLockState() == desiredLockState)
            {
                return;
            }

            runtimeGateway.SetLockState(desiredLockState);
        }
    }

    internal sealed class UnityCursorConfinementRuntimeGateway : ICursorConfinementRuntimeGateway
    {
        public bool SupportsConfinement
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return true;
#elif UNITY_STANDALONE_LINUX && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public bool IsApplicationFocused => Application.isFocused;

        public DisplayWindowMode ReadCurrentWindowMode()
        {
            return Screen.fullScreenMode == FullScreenMode.Windowed
                ? DisplayWindowMode.Windowed
                : DisplayWindowMode.FullScreenWindow;
        }

        public CursorLockMode ReadCurrentLockState()
        {
            return Cursor.lockState;
        }

        public void SetLockState(CursorLockMode lockState)
        {
            Cursor.lockState = lockState;
        }
    }
}
