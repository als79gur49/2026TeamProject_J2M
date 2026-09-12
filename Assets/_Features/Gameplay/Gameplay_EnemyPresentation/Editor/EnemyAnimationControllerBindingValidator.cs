using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Host;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    internal enum EnemyAnimationBindingDiagnosticSeverity
    {
        Warning = 0,
        Error = 1,
    }

    internal readonly struct EnemyAnimationBindingDiagnostic
    {
        public EnemyAnimationBindingDiagnostic(
            string code,
            EnemyAnimationBindingDiagnosticSeverity severity,
            string message)
        {
            Code = code;
            Severity = severity;
            Message = message;
        }

        public string Code { get; }

        public EnemyAnimationBindingDiagnosticSeverity Severity { get; }

        public string Message { get; }
    }

    internal static class EnemyAnimationControllerBindingValidator
    {
        internal static IReadOnlyList<EnemyAnimationBindingDiagnostic> Validate(
            EnemyAnimationBindingAuthoring authoring,
            Animator animator)
        {
            var diagnostics = new List<EnemyAnimationBindingDiagnostic>();
            if (authoring == null)
            {
                AddError(diagnostics, "authoring.missing", "Animation binding authoring is missing.");
                return diagnostics;
            }

            if (!ValidateAuthoringPlacement(authoring, diagnostics))
            {
                return diagnostics;
            }

            EnemyAnimationBindingSnapshot snapshot;
            try
            {
                snapshot = authoring.CreateSnapshot();
            }
            catch (Exception exception)
            {
                AddError(diagnostics, "authoring.invalid", exception.Message);
                return diagnostics;
            }

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                AddError(diagnostics, "controller.missing", "A runtime Animator Controller is required.");
                return diagnostics;
            }

            var assignedController = animator.runtimeAnimatorController;
            if (!TryUnwrapController(assignedController, out var controller, out var unwrapError))
            {
                AddError(diagnostics, "controller.unwrap", unwrapError);
                return diagnostics;
            }

            ValidateParameters(controller, diagnostics);
            var layerZeroStateNames = CollectStateNames(controller, 0);
            var otherLayerStateNames = new HashSet<string>(StringComparer.Ordinal);
            for (var layerIndex = 1; layerIndex < controller.layers.Length; layerIndex++)
            {
                foreach (var stateName in CollectStateNames(controller, layerIndex).Keys)
                {
                    otherLayerStateNames.Add(stateName);
                }
            }

            var effectiveTriggerParameters = CollectEffectiveTransitionParameters(controller);
            var effectiveClips = new HashSet<AnimationClip>(assignedController.animationClips);
            foreach (var binding in snapshot.Bindings)
            {
                if (binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.Trigger)
                {
                    ValidateTrigger(binding.TargetName, controller, effectiveTriggerParameters, diagnostics);
                    if (binding.Cue == EnemyAnimationCue.JumpAirborne)
                    {
                        ValidateState(
                            binding.SustainedStateName,
                            animator,
                            layerZeroStateNames,
                            otherLayerStateNames,
                            diagnostics);
                    }
                }
                else if (binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.State)
                {
                    ValidateState(
                        binding.TargetName,
                        animator,
                        layerZeroStateNames,
                        otherLayerStateNames,
                        diagnostics);
                }

                if (!string.IsNullOrWhiteSpace(binding.ReplacementStateName))
                {
                    ValidateState(
                        binding.ReplacementStateName,
                        animator,
                        layerZeroStateNames,
                        otherLayerStateNames,
                        diagnostics);
                }

                if (binding.ReferenceClip != null && !effectiveClips.Contains(binding.ReferenceClip))
                {
                    AddWarning(
                        diagnostics,
                        "clip.not-effective",
                        $"{binding.Cue} timing reference clip is not an effective clip of the assigned controller. " +
                        "Validate the target state's effective motion separately.");
                }
            }

            return diagnostics;
        }

        internal static bool TryUnwrapController(
            RuntimeAnimatorController assignedController,
            out AnimatorController controller,
            out string error)
        {
            var visited = new HashSet<RuntimeAnimatorController>();
            var current = assignedController;
            while (current is AnimatorOverrideController overrideController)
            {
                if (!visited.Add(current))
                {
                    controller = null;
                    error = "Animator Override Controller chain contains a cycle.";
                    return false;
                }

                current = overrideController.runtimeAnimatorController;
                if (current == null)
                {
                    controller = null;
                    error = "Animator Override Controller has no base controller.";
                    return false;
                }
            }

            controller = current as AnimatorController;
            if (controller == null)
            {
                error = "Assigned controller does not unwrap to an AnimatorController.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void ValidateParameters(
            AnimatorController controller,
            ICollection<EnemyAnimationBindingDiagnostic> diagnostics)
        {
            foreach (var group in controller.parameters.GroupBy(parameter => parameter.name, StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                {
                    AddError(
                        diagnostics,
                        "parameter.duplicate",
                        $"Animator parameter '{group.Key}' is declared more than once.");
                }
            }
        }

        private static void ValidateTrigger(
            string parameterName,
            AnimatorController controller,
            ISet<string> effectiveTransitionParameters,
            ICollection<EnemyAnimationBindingDiagnostic> diagnostics)
        {
            var matching = controller.parameters
                .Where(parameter => string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
                .ToArray();
            if (matching.Length == 0)
            {
                AddError(diagnostics, "trigger.missing", $"Trigger parameter '{parameterName}' is missing.");
                return;
            }

            if (matching.Length != 1 || matching[0].type != AnimatorControllerParameterType.Trigger)
            {
                AddError(
                    diagnostics,
                    "trigger.wrong-type",
                    $"Animator parameter '{parameterName}' must be exactly one Trigger parameter.");
                return;
            }

            if (!effectiveTransitionParameters.Contains(parameterName))
            {
                AddError(
                    diagnostics,
                    "trigger.unconsumed",
                    $"Trigger '{parameterName}' is not consumed by an effective transition.");
            }
        }

        private static void ValidateState(
            string stateName,
            Animator animator,
            IReadOnlyDictionary<string, int> layerZeroStateNames,
            ISet<string> otherLayerStateNames,
            ICollection<EnemyAnimationBindingDiagnostic> diagnostics)
        {
            layerZeroStateNames.TryGetValue(stateName, out var count);
            if (count > 1)
            {
                AddError(
                    diagnostics,
                    "state.ambiguous",
                    $"State '{stateName}' appears more than once on layer 0.");
                return;
            }

            if (count == 0)
            {
                AddError(
                    diagnostics,
                    otherLayerStateNames.Contains(stateName) ? "state.layer1-only" : "state.missing",
                    otherLayerStateNames.Contains(stateName)
                        ? $"State '{stateName}' exists only outside layer 0."
                        : $"State '{stateName}' is missing from layer 0.");
                return;
            }

            if (!EnemyAnimatorStateNameResolver.TryResolveLayerZeroStateHash(animator, stateName, out _))
            {
                AddError(
                    diagnostics,
                    "state.runtime-unresolved",
                    $"State '{stateName}' exists in the controller graph but cannot be resolved by the runtime layer-0 resolver.");
            }
        }

        private static Dictionary<string, int> CollectStateNames(AnimatorController controller, int layerIndex)
        {
            var stateNames = new Dictionary<string, int>(StringComparer.Ordinal);
            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
            {
                return stateNames;
            }

            CollectStateNames(controller.layers[layerIndex].stateMachine, stateNames);
            return stateNames;
        }

        private static void CollectStateNames(
            AnimatorStateMachine stateMachine,
            IDictionary<string, int> stateNames)
        {
            foreach (var childState in stateMachine.states)
            {
                stateNames.TryGetValue(childState.state.name, out var count);
                stateNames[childState.state.name] = count + 1;
            }

            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                CollectStateNames(childStateMachine.stateMachine, stateNames);
            }
        }

        private static HashSet<string> CollectEffectiveTransitionParameters(AnimatorController controller)
        {
            var parameterNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var layer in controller.layers)
            {
                CollectEffectiveTransitionParameters(layer.stateMachine, parameterNames);
            }

            return parameterNames;
        }

        private static void CollectEffectiveTransitionParameters(
            AnimatorStateMachine stateMachine,
            ISet<string> parameterNames)
        {
            AddEffectiveTransitionConditions(stateMachine.anyStateTransitions, parameterNames);
            AddEffectiveTransitionConditions(stateMachine.entryTransitions, parameterNames);
            foreach (var childState in stateMachine.states)
            {
                AddEffectiveTransitionConditions(childState.state.transitions, parameterNames);
            }

            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                AddEffectiveTransitionConditions(
                    stateMachine.GetStateMachineTransitions(childStateMachine.stateMachine),
                    parameterNames);
                CollectEffectiveTransitionParameters(childStateMachine.stateMachine, parameterNames);
            }
        }

        private static bool ValidateAuthoringPlacement(
            EnemyAnimationBindingAuthoring authoring,
            ICollection<EnemyAnimationBindingDiagnostic> diagnostics)
        {
            var driver = authoring.GetComponentInParent<EnemyAnimatorDriver>(includeInactive: true);
            if (driver == null || authoring.transform != driver.transform)
            {
                AddError(
                    diagnostics,
                    "authoring.not-root",
                    "Animation binding authoring must be placed on the enemy View root beside its animator driver.");
                return false;
            }

            var candidates = driver.GetComponentsInChildren<EnemyAnimationBindingAuthoring>(includeInactive: true);
            if (candidates.Length != 1)
            {
                AddError(
                    diagnostics,
                    "authoring.duplicate",
                    $"Enemy View root must contain exactly one animation binding authoring, but found {candidates.Length}.");
                return false;
            }

            if (!authoring.enabled)
            {
                AddError(
                    diagnostics,
                    "authoring.disabled",
                    "Animation binding authoring must be enabled when present.");
                return false;
            }

            return true;
        }

        private static void AddEffectiveTransitionConditions<T>(
            IReadOnlyList<T> transitions,
            ISet<string> parameterNames)
            where T : AnimatorTransitionBase
        {
            var hasSolo = transitions.Any(transition => transition != null && transition.solo && !transition.mute);
            foreach (var transition in transitions)
            {
                if (transition == null || transition.mute || (hasSolo && !transition.solo))
                {
                    continue;
                }

                foreach (var condition in transition.conditions)
                {
                    parameterNames.Add(condition.parameter);
                }
            }
        }

        private static void AddError(
            ICollection<EnemyAnimationBindingDiagnostic> diagnostics,
            string code,
            string message)
        {
            diagnostics.Add(new EnemyAnimationBindingDiagnostic(
                code,
                EnemyAnimationBindingDiagnosticSeverity.Error,
                message));
        }

        private static void AddWarning(
            ICollection<EnemyAnimationBindingDiagnostic> diagnostics,
            string code,
            string message)
        {
            diagnostics.Add(new EnemyAnimationBindingDiagnostic(
                code,
                EnemyAnimationBindingDiagnosticSeverity.Warning,
                message));
        }
    }
}
