using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class EnemySemanticParticleEffectBinding
    {
        [SerializeField] private ParticleSystem particleSystem;
        [SerializeField] private ParticleSystemRenderer renderer;
        [SerializeField] private EnemyPresentationEffectInactivePolicy policy =
            EnemyPresentationEffectInactivePolicy.StopOnInactiveResumeOnNormal;
        [SerializeField] private bool includeChildren = true;
        [SerializeField] private bool clearOnInactive = true;
        [SerializeField] private bool restoreRendererOnNormal = true;

        public EnemySemanticParticleEffectBinding()
        {
        }

        public EnemySemanticParticleEffectBinding(
            ParticleSystem particleSystem,
            EnemyPresentationEffectInactivePolicy policy,
            ParticleSystemRenderer renderer = null,
            bool includeChildren = true,
            bool clearOnInactive = true,
            bool restoreRendererOnNormal = true)
        {
            this.particleSystem = particleSystem;
            this.renderer = renderer;
            this.policy = policy;
            this.includeChildren = includeChildren;
            this.clearOnInactive = clearOnInactive;
            this.restoreRendererOnNormal = restoreRendererOnNormal;
        }

        public ParticleSystem ParticleSystem => particleSystem;

        public ParticleSystemRenderer Renderer => renderer;

        public EnemyPresentationEffectInactivePolicy Policy => policy;

        public bool IncludeChildren => includeChildren;

        public bool ClearOnInactive => clearOnInactive;

        public bool RestoreRendererOnNormal => restoreRendererOnNormal;
    }

    public sealed class EnemySemanticParticleEffectController :
        MonoBehaviour,
        IEnemyVisualSemanticPresentationDriver,
        IEnemyPresentationParticleEffectOwner
    {
        [SerializeField] private EnemySemanticParticleEffectBinding[] bindings =
            Array.Empty<EnemySemanticParticleEffectBinding>();

        private BindingRuntimeState[] _states = Array.Empty<BindingRuntimeState>();

        public int BindingCount => bindings?.Length ?? 0;

        public void Apply(in EnemyVisualSemanticState state)
        {
            ApplyEnemyVisualSemanticState(state);
        }

        public void ApplyEnemyVisualSemanticState(in EnemyVisualSemanticState state)
        {
            EnsureStateStorage();

            if (state.ActivityState == EnemyVisualActivityState.FrontFaceInactive)
            {
                ApplyInactive();
                return;
            }

            ApplyNormal();
        }

        public void CollectManagedParticleSystems(ICollection<ParticleSystem> particleSystems)
        {
            if (particleSystems == null || bindings == null)
            {
                return;
            }

            for (var i = 0; i < bindings.Length; i++)
            {
                var particleSystem = bindings[i]?.ParticleSystem;
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystems.Add(particleSystem);
                if (!bindings[i].IncludeChildren)
                {
                    continue;
                }

                var children = particleSystem.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
                for (var childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    if (children[childIndex] != null)
                    {
                        particleSystems.Add(children[childIndex]);
                    }
                }
            }
        }

        private void OnDisable()
        {
            RestoreSuppressedRenderers();
            ClearRuntimeState();
        }

        private void ApplyInactive()
        {
            if (bindings == null)
            {
                return;
            }

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null || binding.ParticleSystem == null)
                {
                    continue;
                }

                switch (binding.Policy)
                {
                    case EnemyPresentationEffectInactivePolicy.IgnoreInactiveSemantic:
                        break;
                    case EnemyPresentationEffectInactivePolicy.StopOnInactiveResumeOnNormal:
                        StopBinding(i, binding, resumeOnNormal: true);
                        break;
                    case EnemyPresentationEffectInactivePolicy.StopOnInactiveDoNotResume:
                        StopBinding(i, binding, resumeOnNormal: false);
                        break;
                    case EnemyPresentationEffectInactivePolicy.HideRendererOnly:
                        HideRenderer(i, binding);
                        break;
                    default:
                        StopBinding(i, binding, resumeOnNormal: true);
                        break;
                }
            }
        }

        private void ApplyNormal()
        {
            if (bindings == null)
            {
                return;
            }

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                ref var state = ref _states[i];
                if (state.RendererSuppressedByController)
                {
                    RestoreRenderer(binding, ref state);
                }

                if (!state.ParticleStoppedByController)
                {
                    continue;
                }

                if (state.ResumeOnNormal && binding.ParticleSystem != null)
                {
                    binding.ParticleSystem.Play(binding.IncludeChildren);
                }

                state.ParticleStoppedByController = false;
                state.ResumeOnNormal = false;
            }
        }

        private void StopBinding(
            int index,
            EnemySemanticParticleEffectBinding binding,
            bool resumeOnNormal)
        {
            ref var state = ref _states[index];
            if (state.ParticleStoppedByController)
            {
                return;
            }

            var particleSystem = binding.ParticleSystem;
            state.ResumeOnNormal = resumeOnNormal && ShouldResumeAfterStop(particleSystem, binding.IncludeChildren);
            state.ParticleStoppedByController = true;

            particleSystem.Stop(
                binding.IncludeChildren,
                binding.ClearOnInactive
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
        }

        private void HideRenderer(int index, EnemySemanticParticleEffectBinding binding)
        {
            ref var state = ref _states[index];
            if (state.RendererSuppressedByController)
            {
                return;
            }

            var renderer = ResolveRenderer(binding);
            if (renderer == null)
            {
                return;
            }

            state.RendererSuppressedByController = true;
            state.PreviousRendererEnabled = renderer.enabled;
            renderer.enabled = false;
        }

        private void RestoreRenderer(
            EnemySemanticParticleEffectBinding binding,
            ref BindingRuntimeState state)
        {
            var renderer = ResolveRenderer(binding);
            if (renderer != null && binding.RestoreRendererOnNormal)
            {
                renderer.enabled = state.PreviousRendererEnabled;
            }

            state.RendererSuppressedByController = false;
            state.PreviousRendererEnabled = false;
        }

        private ParticleSystemRenderer ResolveRenderer(EnemySemanticParticleEffectBinding binding)
        {
            if (binding.Renderer != null)
            {
                return binding.Renderer;
            }

            return binding.ParticleSystem != null
                ? binding.ParticleSystem.GetComponent<ParticleSystemRenderer>()
                : null;
        }

        private static bool ShouldResumeAfterStop(ParticleSystem particleSystem, bool includeChildren)
        {
            if (particleSystem == null)
            {
                return false;
            }

            if (particleSystem.isPlaying || particleSystem.isEmitting || particleSystem.IsAlive(withChildren: false))
            {
                return true;
            }

            if (!includeChildren)
            {
                return false;
            }

            var children = particleSystem.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child != null &&
                    child != particleSystem &&
                    (child.isPlaying || child.isEmitting || child.IsAlive(withChildren: false)))
                {
                    return true;
                }
            }

            return false;
        }

        private void RestoreSuppressedRenderers()
        {
            if (bindings == null || _states == null)
            {
                return;
            }

            var count = Math.Min(bindings.Length, _states.Length);
            for (var i = 0; i < count; i++)
            {
                if (_states[i].RendererSuppressedByController && bindings[i] != null)
                {
                    RestoreRenderer(bindings[i], ref _states[i]);
                }
            }
        }

        private void ClearRuntimeState()
        {
            if (_states == null)
            {
                _states = Array.Empty<BindingRuntimeState>();
                return;
            }

            Array.Clear(_states, 0, _states.Length);
        }

        private void EnsureStateStorage()
        {
            var expectedLength = bindings?.Length ?? 0;
            if (_states != null && _states.Length == expectedLength)
            {
                return;
            }

            _states = expectedLength == 0
                ? Array.Empty<BindingRuntimeState>()
                : new BindingRuntimeState[expectedLength];
        }

        private struct BindingRuntimeState
        {
            public bool ParticleStoppedByController;
            public bool ResumeOnNormal;
            public bool RendererSuppressedByController;
            public bool PreviousRendererEnabled;
        }
    }
}
