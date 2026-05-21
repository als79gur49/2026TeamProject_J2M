using System;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class VfxLifetimeRunner
    {
        public void Stop(IVfxPlaybackHandle handle, VfxStopPolicy stopPolicy)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            switch (stopPolicy)
            {
                case VfxStopPolicy.DetachThenStopEmittingThenRelease:
                    handle.Detach();
                    handle.StopEmitting();
                    handle.MarkTailPlaying();
                    break;
                case VfxStopPolicy.StopEmittingThenRelease:
                case VfxStopPolicy.NaturalCompletion:
                case VfxStopPolicy.AuthoredDuration:
                    handle.StopEmitting();
                    handle.MarkTailPlaying();
                    break;
                case VfxStopPolicy.ManualStopRequired:
                case VfxStopPolicy.HardCleanupOnly:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stopPolicy), stopPolicy, null);
            }
        }

        public void Stop(IVfxPlaybackHandle handle, GameplayVfxStopMode stopMode)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            handle.Stop(stopMode);
        }

        public void AdvanceTail(IVfxPlaybackHandle handle, bool tailComplete, IVfxPool pool)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (!tailComplete || handle.State != VfxLifetimeState.TailPlaying)
            {
                return;
            }

            handle.ReleaseToPool();
            pool.Release(handle);
        }

        public void HardCleanup(IVfxPlaybackHandle handle, IVfxPool pool)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            handle.HardCleanup();
            pool.Release(handle);
        }
    }
}
