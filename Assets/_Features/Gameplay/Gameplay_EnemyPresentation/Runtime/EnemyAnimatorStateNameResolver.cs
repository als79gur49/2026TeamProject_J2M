using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal static class EnemyAnimatorStateNameResolver
    {
        private const string BaseLayerPrefix = "Base Layer.";
        private const string LocomotionPrefix = "Base Layer.Locomotion.";

        internal static bool TryResolveLayerZeroStateHash(
            Animator animator,
            string stateName,
            out int stateHash)
        {
            if (animator == null ||
                animator.runtimeAnimatorController == null ||
                string.IsNullOrWhiteSpace(stateName))
            {
                stateHash = 0;
                return false;
            }

            var shortNameHash = Animator.StringToHash(stateName);
            if (animator.HasState(0, shortNameHash))
            {
                stateHash = shortNameHash;
                return true;
            }

            var rootStateHash = Animator.StringToHash(BaseLayerPrefix + stateName);
            if (animator.HasState(0, rootStateHash))
            {
                stateHash = rootStateHash;
                return true;
            }

            var locomotionStateHash = Animator.StringToHash(LocomotionPrefix + stateName);
            if (animator.HasState(0, locomotionStateHash))
            {
                stateHash = locomotionStateHash;
                return true;
            }

            stateHash = 0;
            return false;
        }

        internal static int ResolveLayerZeroStateHashOrFallback(Animator animator, string stateName)
        {
            return TryResolveLayerZeroStateHash(animator, stateName, out var stateHash)
                ? stateHash
                : Animator.StringToHash(stateName ?? string.Empty);
        }

        internal static bool IsLayerZeroState(in AnimatorStateInfo stateInfo, string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            return stateInfo.IsName(stateName) ||
                   stateInfo.IsName(BaseLayerPrefix + stateName) ||
                   stateInfo.IsName(LocomotionPrefix + stateName) ||
                   stateInfo.shortNameHash == Animator.StringToHash(stateName);
        }

        internal static string GetRootPath(string stateName)
        {
            return BaseLayerPrefix + stateName;
        }

        internal static string GetLocomotionPath(string stateName)
        {
            return LocomotionPrefix + stateName;
        }
    }
}
