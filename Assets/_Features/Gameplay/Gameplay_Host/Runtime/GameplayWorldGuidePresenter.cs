using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Stages;
using Game.Shared.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    public interface IWorldGuideLocalizationSource
    {
        event Action GuidesChanged;

        void CopyLocalizationTargets(List<IWorldGuideLocalizationTarget> destination);
    }

    [DisallowMultipleComponent]
    public sealed class GameplayWorldGuidePresenter : MonoBehaviour, IWorldGuideLocalizationSource
    {
        private sealed class GuideInstance
        {
            public StageWorldGuideInstructionResolved Instruction;
            public WorldGuideInstructionView View;
            public Transform Transform;
            public Vector3 AuthoredLocalScale;
        }

        private readonly List<GuideInstance> _instances = new();
        private GameplayBoardSurfaceRenderer _boardSurfaceRenderer;
        private ISurfaceCellPresentationPoseResolver _poseResolver;
        private Camera _viewCamera;
        private Transform _parent;
        private KeyboardBindingSettingsService _keyboardBindingSettingsService;
        private KeyboardBindingSettingsSnapshot _keyboardBindingSnapshot;
        private bool _initialized;

        public event Action GuidesChanged;

        public int InstanceCount => _instances.Count;

        public void CopyLocalizationTargets(List<IWorldGuideLocalizationTarget> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            for (var i = 0; i < _instances.Count; i++)
            {
                if (_instances[i].View != null)
                {
                    destination.Add(_instances[i].View);
                }
            }
        }

        public void Initialize(
            StageWorldGuideCatalog catalog,
            IReadOnlyList<StageWorldGuideInstructionResolved> instructions,
            GameplayBoardSurfaceRenderer boardSurfaceRenderer,
            ISurfaceCellPresentationPoseResolver poseResolver,
            Camera viewCamera,
            Transform parent,
            InputActionAsset actions = null,
            IKeyboardBindingStore bindingStore = null)
        {
            Cleanup();

            _boardSurfaceRenderer = boardSurfaceRenderer;
            _poseResolver = poseResolver;
            _viewCamera = viewCamera;
            _parent = parent != null ? parent : transform;
            ConfigureKeyboardBindings(actions, bindingStore);
            _initialized = true;

            if (catalog == null || instructions == null || instructions.Count == 0)
            {
                return;
            }

            for (var i = 0; i < instructions.Count; i++)
            {
                InstantiateGuide(catalog, instructions[i], i);
            }

            GuidesChanged?.Invoke();
            RefreshAll();
        }

        public void Cleanup()
        {
            var hadInstances = _instances.Count > 0;
            for (var i = 0; i < _instances.Count; i++)
            {
                var view = _instances[i].View;
                if (view == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(view.gameObject);
                }
                else
                {
                    DestroyImmediate(view.gameObject);
                }
            }

            _instances.Clear();
            KeyboardBindingSettingsService.BindingsChanged -= HandleKeyboardBindingsChanged;
            _keyboardBindingSettingsService?.Dispose();
            _keyboardBindingSettingsService = null;
            _initialized = false;
            if (hadInstances)
            {
                GuidesChanged?.Invoke();
            }
        }

        private void LateUpdate()
        {
            if (_initialized)
            {
                RefreshAll();
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void InstantiateGuide(
            StageWorldGuideCatalog catalog,
            StageWorldGuideInstructionResolved instruction,
            int index)
        {
            if (!catalog.TryResolve(instruction.GuideKey, out var entry) ||
                entry == null ||
                entry.Prefab == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"Skipping StageWorldGuideInstruction '{instruction.GuideKey}' at index {index}; no catalog prefab resolved.",
                    this);
                return;
            }

            var instance = Instantiate(entry.Prefab, _parent, worldPositionStays: false);
            instance.name = $"{entry.Prefab.name}_{instruction.GuideKey}";
            if (!instance.TryGetComponent<WorldGuideInstructionView>(out var view))
            {
                view = instance.GetComponentInChildren<WorldGuideInstructionView>(includeInactive: true);
            }

            if (view == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"Skipping StageWorldGuideInstruction '{instruction.GuideKey}'; prefab '{entry.Prefab.name}' has no {nameof(WorldGuideInstructionView)}.",
                    this);
                Destroy(instance);
                return;
            }

            view.ApplyKeyboardBindings(_keyboardBindingSnapshot);
            _instances.Add(new GuideInstance
            {
                Instruction = instruction,
                View = view,
                Transform = view.transform,
                AuthoredLocalScale = view.transform.localScale,
            });
        }

        private void RefreshAll()
        {
            for (var i = 0; i < _instances.Count; i++)
            {
                RefreshInstance(_instances[i]);
            }
        }

        private void RefreshInstance(GuideInstance instance)
        {
            if (instance?.View == null || instance.Transform == null)
            {
                return;
            }

            if (!TryResolvePose(instance.Instruction, out var pose))
            {
                instance.View.SetVisible(false);
                return;
            }

            instance.Transform.localPosition = pose.LocalPosition;
            instance.Transform.localRotation = ResolveRotation(instance.Instruction.FacingMode, pose.LocalRotation, pose.LocalPosition);
            instance.Transform.localScale = Vector3.Scale(pose.LocalScale, instance.AuthoredLocalScale);
            instance.View.SetVisible(true);
        }

        private void ConfigureKeyboardBindings(
            InputActionAsset actions,
            IKeyboardBindingStore bindingStore)
        {
            _keyboardBindingSnapshot = new KeyboardBindingSettingsSnapshot(
                KeyboardMovementScheme.Wasd,
                "WASD",
                "J",
                "K",
                isRebinding: false,
                rebindingAction: null);

            if (actions == null)
            {
                return;
            }

            _keyboardBindingSettingsService = new KeyboardBindingSettingsService(actions, bindingStore);
            _keyboardBindingSnapshot = _keyboardBindingSettingsService.Read();
            KeyboardBindingSettingsService.BindingsChanged += HandleKeyboardBindingsChanged;
        }

        private void HandleKeyboardBindingsChanged(KeyboardBindingSettingsSnapshot snapshot)
        {
            _keyboardBindingSnapshot = snapshot;
            for (var i = 0; i < _instances.Count; i++)
            {
                _instances[i].View?.ApplyKeyboardBindings(snapshot);
            }
        }

        private bool TryResolvePose(
            StageWorldGuideInstructionResolved instruction,
            out SurfaceCellPresentationPose pose)
        {
            pose = default;
            if (_poseResolver == null)
            {
                return false;
            }

            if (_boardSurfaceRenderer != null &&
                _boardSurfaceRenderer.IsTopologyTransitionActive &&
                instruction.HideWhenFaceInactive)
            {
                return false;
            }

            if (instruction.HideWhenFaceInactive &&
                (_boardSurfaceRenderer == null ||
                 !_boardSurfaceRenderer.TryGetTileVisualHandle(instruction.Cell, out _)))
            {
                return false;
            }

            if (_poseResolver is BoardSurfaceCellPresentationPoseResolver boardPoseResolver &&
                _boardSurfaceRenderer != null)
            {
                boardPoseResolver.RefreshTopology(_boardSurfaceRenderer.SteadyTopology);
            }

            if (!_poseResolver.TryResolvePose(instruction.Cell, out var basePose))
            {
                return false;
            }

            var normal = -(basePose.LocalRotation * Vector3.forward);
            var surfaceOffset = basePose.SurfaceOutwardOffset + instruction.HeightOffset;
            var localPosition = basePose.LocalPosition +
                                (normal * surfaceOffset) +
                                (basePose.LocalRotation * instruction.LocalOffset);
            pose = new SurfaceCellPresentationPose(localPosition, basePose.LocalRotation, basePose.LocalScale);
            return true;
        }

        private Quaternion ResolveRotation(
            StageWorldGuideFacingMode facingMode,
            Quaternion surfaceRotation,
            Vector3 localPosition)
        {
            switch (facingMode)
            {
                case StageWorldGuideFacingMode.SurfaceAligned:
                    return surfaceRotation;
                case StageWorldGuideFacingMode.YawOnlyBillboard:
                    return ResolveYawOnlyBillboardRotation(localPosition, surfaceRotation);
                case StageWorldGuideFacingMode.BillboardToCamera:
                default:
                    return ResolveBillboardRotation(localPosition, surfaceRotation);
            }
        }

        private Quaternion ResolveBillboardRotation(Vector3 localPosition, Quaternion fallback)
        {
            var cameraTransform = ResolveCameraTransform();
            if (cameraTransform == null || _parent == null)
            {
                return fallback;
            }

            var worldPosition = _parent.TransformPoint(localPosition);
            var toCamera = worldPosition - cameraTransform.position;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return fallback;
            }

            return Quaternion.Inverse(_parent.rotation) *
                   Quaternion.LookRotation(toCamera.normalized, cameraTransform.up);
        }

        private Quaternion ResolveYawOnlyBillboardRotation(Vector3 localPosition, Quaternion fallback)
        {
            var cameraTransform = ResolveCameraTransform();
            if (cameraTransform == null || _parent == null)
            {
                return fallback;
            }

            var worldPosition = _parent.TransformPoint(localPosition);
            var toCamera = worldPosition - cameraTransform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return fallback;
            }

            return Quaternion.Inverse(_parent.rotation) *
                   Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        private Transform ResolveCameraTransform()
        {
            if (_viewCamera != null)
            {
                return _viewCamera.transform;
            }

            _viewCamera = Camera.main;
            return _viewCamera != null ? _viewCamera.transform : null;
        }
    }
}
