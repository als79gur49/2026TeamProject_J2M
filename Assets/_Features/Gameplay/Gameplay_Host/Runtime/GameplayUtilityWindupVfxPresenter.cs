using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayUtilityWindupVfxPresenter
    {
        internal int SummonWarningInstanceCount => 0;

        public void Initialize(Transform parent)
        {
        }

        public void Clear()
        {
        }

        public void RefreshSummonWarnings(
            IReadOnlyList<TickSummonWindupWarningSignal> signals,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            if (signals == null)
            {
                throw new ArgumentNullException(nameof(signals));
            }

            if (stateStore == null)
            {
                throw new ArgumentNullException(nameof(stateStore));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }
        }
    }
}
