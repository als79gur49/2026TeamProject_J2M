using Game.Shared.Input;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Feature.Gameplay.Host
{
    public enum WorldGuideInstructionKind
    {
        None = 0,
        Movement = 1,
        Push = 2,
        Flip = 3,
    }

    public interface IWorldGuideLocalizationTarget
    {
        WorldGuideInstructionKind InstructionKind { get; }

        GameObject WasdDisplayRoot { get; }

        GameObject ArrowDisplayRoot { get; }

        TMP_Text ActionKeyLabel { get; }

        TMP_Text ActionTextLabel { get; }

        void ApplyActionText(string resolvedActionText);
    }

    [DisallowMultipleComponent]
    public sealed class WorldGuideInstructionView : MonoBehaviour, IWorldGuideLocalizationTarget
    {
        [SerializeField] private Canvas canvas;
        [FormerlySerializedAs("bindingKind")]
        [SerializeField] private WorldGuideInstructionKind instructionKind = WorldGuideInstructionKind.None;
        [SerializeField] private GameObject wasdDisplayRoot;
        [SerializeField] private GameObject arrowDisplayRoot;
        [SerializeField] private TMP_Text actionKeyLabel;
        [SerializeField] private TMP_Text actionTextLabel;

        public WorldGuideInstructionKind InstructionKind => instructionKind;

        public GameObject WasdDisplayRoot => wasdDisplayRoot;

        public GameObject ArrowDisplayRoot => arrowDisplayRoot;

        public TMP_Text ActionKeyLabel => actionKeyLabel;

        public TMP_Text ActionTextLabel => actionTextLabel;

        public void ApplyKeyboardBindings(KeyboardBindingSettingsSnapshot snapshot)
        {
            switch (instructionKind)
            {
                case WorldGuideInstructionKind.Movement:
                    SetMovementScheme(snapshot.MovementScheme);
                    break;
                case WorldGuideInstructionKind.Push:
                    SetActionKey(snapshot.PushDisplayName);
                    break;
                case WorldGuideInstructionKind.Flip:
                    SetActionKey(snapshot.FlipDisplayName);
                    break;
            }
        }

        public void ApplyActionText(string resolvedActionText)
        {
            if (actionTextLabel != null)
            {
                actionTextLabel.text = resolvedActionText ?? string.Empty;
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void Awake()
        {
            ValidateReferences();
            EnsureWorldSpaceCanvas();
        }

        private void OnValidate()
        {
            EnsureWorldSpaceCanvas();
        }

        private void EnsureWorldSpaceCanvas()
        {
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
            }
        }

        private void ValidateReferences()
        {
            if (instructionKind == WorldGuideInstructionKind.Movement &&
                wasdDisplayRoot == null &&
                arrowDisplayRoot == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"{nameof(WorldGuideInstructionView)} on '{name}' has Movement binding but no movement display roots.",
                    this);
            }

            if ((instructionKind == WorldGuideInstructionKind.Push ||
                 instructionKind == WorldGuideInstructionKind.Flip) &&
                actionKeyLabel == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"{nameof(WorldGuideInstructionView)} on '{name}' has {instructionKind} instruction but no action key label.",
                    this);
            }

            if (instructionKind != WorldGuideInstructionKind.None && actionTextLabel == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"{nameof(WorldGuideInstructionView)} on '{name}' has {instructionKind} instruction but no action text label.",
                    this);
            }
        }

        private void SetMovementScheme(KeyboardMovementScheme movementScheme)
        {
            if (wasdDisplayRoot != null)
            {
                wasdDisplayRoot.SetActive(movementScheme == KeyboardMovementScheme.Wasd);
            }

            if (arrowDisplayRoot != null)
            {
                arrowDisplayRoot.SetActive(movementScheme == KeyboardMovementScheme.ArrowKeys);
            }
        }

        private void SetActionKey(string displayName)
        {
            if (actionKeyLabel != null)
            {
                actionKeyLabel.text = displayName ?? string.Empty;
            }
        }
    }
}
