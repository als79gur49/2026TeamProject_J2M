using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Feature.Gameplay.Vfx.Authoring
{
    public static class VfxPrefabValidationDiagnostics
    {
        public const string ModelRootName = "ModelRoot";
        private const float TransformTolerance = 0.0001f;

        public static VfxAuthoringValidationResult ValidatePrefab(
            GameObject prefab,
            UnityEngine.Object context = null)
        {
            var messages = new List<VfxAuthoringValidationMessage>();
            AppendPrefabMessages(prefab, messages, context);
            return VfxAuthoringValidationResult.FromMessages(messages);
        }

        public static VfxAuthoringValidationResult ValidateModelRootContract(
            GameObject prefab,
            UnityEngine.Object context = null)
        {
            var messages = new List<VfxAuthoringValidationMessage>();
            AppendModelRootContractMessages(prefab, messages, context);
            return VfxAuthoringValidationResult.FromMessages(messages);
        }

        public static void AppendModelRootContractMessages(
            GameObject prefab,
            ICollection<VfxAuthoringValidationMessage> messages,
            UnityEngine.Object context = null)
        {
            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            if (prefab == null)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_PREFAB_NULL",
                    "VFX binding prefab cannot be null.",
                    context));
                return;
            }

            var messageContext = context != null ? context : prefab;
            var root = prefab.transform;
            if (!IsRootTransformIdentity(root))
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_PREFAB_RUNTIME_ROOT_POSE",
                    $"{prefab.name} VFX prefab root must keep identity local transform; runtime anchors own the root pose.",
                    messageContext));
            }

            var modelRoot = root.Find(ModelRootName);
            if (modelRoot == null || modelRoot.parent != root)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_PREFAB_MODEL_ROOT_MISSING",
                    $"{prefab.name} VFX prefab must contain a direct child named '{ModelRootName}'.",
                    messageContext));
            }

            var rootComponents = root.GetComponents<Component>();
            for (var i = 0; i < rootComponents.Length; i++)
            {
                var component = rootComponents[i];
                if (component == null || component is Transform)
                {
                    continue;
                }

                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_PREFAB_ROOT_COMPONENT",
                    $"{prefab.name} VFX prefab root contains '{component.GetType().Name}'. Visual components must live under '{ModelRootName}'.",
                    messageContext));
            }
        }

        public static void AppendPrefabMessages(
            GameObject prefab,
            ICollection<VfxAuthoringValidationMessage> messages,
            UnityEngine.Object context = null)
        {
            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            if (prefab == null)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_PREFAB_NULL",
                    "VFX binding prefab cannot be null.",
                    context));
                return;
            }

            var components = prefab.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    messages.Add(VfxAuthoringValidationResult.Warning(
                        "VFX_PREFAB_MISSING_COMPONENT",
                        $"{prefab.name} contains a missing component or script reference.",
                        context != null ? context : prefab));
                    continue;
                }

                AppendComponentMessages(prefab, component, messages, context);
            }
        }

        private static void AppendComponentMessages(
            GameObject prefab,
            Component component,
            ICollection<VfxAuthoringValidationMessage> messages,
            UnityEngine.Object context)
        {
            var messageContext = context != null ? context : prefab;
            var componentPath = GetHierarchyPath(component.transform, prefab.transform);
            var componentName = component.GetType().Name;

            if (component is Collider)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_PREFAB_COLLIDER",
                    $"{prefab.name} VFX prefab contains forbidden Collider '{componentName}' at '{componentPath}'.",
                    messageContext));
                return;
            }

            if (component is AudioSource)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_PREFAB_AUDIO_SOURCE",
                    $"{prefab.name} VFX prefab contains forbidden AudioSource at '{componentPath}'.",
                    messageContext));
                return;
            }

            if (component is NavMeshAgent)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_PREFAB_NAV_MESH_AGENT",
                    $"{prefab.name} VFX prefab contains forbidden NavMeshAgent at '{componentPath}'.",
                    messageContext));
                return;
            }

            if (component is Rigidbody rigidbody)
            {
                if (!rigidbody.isKinematic)
                {
                    messages.Add(VfxAuthoringValidationResult.Error(
                        "VFX_PREFAB_RIGIDBODY_DYNAMIC",
                        $"{prefab.name} VFX prefab contains non-kinematic Rigidbody at '{componentPath}'.",
                        messageContext));
                    return;
                }

                messages.Add(VfxAuthoringValidationResult.Warning(
                    "VFX_PREFAB_RIGIDBODY_KINEMATIC",
                    $"{prefab.name} VFX prefab contains kinematic Rigidbody at '{componentPath}'. VFX prefabs should avoid physics bodies.",
                    messageContext));
                return;
            }

            if (IsAllowedPresentationComponent(component))
            {
                return;
            }

            if (component is MonoBehaviour)
            {
                messages.Add(VfxAuthoringValidationResult.Warning(
                    "VFX_PREFAB_MONO_BEHAVIOUR",
                    $"{prefab.name} VFX prefab contains non-allowlisted MonoBehaviour '{componentName}' at '{componentPath}'.",
                    messageContext));
            }
        }

        private static bool IsAllowedPresentationComponent(Component component)
        {
            return component is Transform
                || component is ParticleSystem
                || component is Renderer
                || component is Animator
                || component is MeshFilter
                || component is Light;
        }

        private static string GetHierarchyPath(Transform transform, Transform root)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            if (transform == root)
            {
                return transform.name;
            }

            var stack = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                stack.Push(current.name);
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            return string.Join("/", stack);
        }

        private static bool IsRootTransformIdentity(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            return Vector3.Distance(root.localPosition, Vector3.zero) <= TransformTolerance
                && Quaternion.Angle(root.localRotation, Quaternion.identity) <= TransformTolerance
                && Vector3.Distance(root.localScale, Vector3.one) <= TransformTolerance;
        }
    }
}
