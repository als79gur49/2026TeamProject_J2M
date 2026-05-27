using Game.Shared.Input;
using TMPro;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum WorldGuideBindingKind
    {
        None = 0,
        Movement = 1,
        Push = 2,
        Flip = 3,
    }

    [DisallowMultipleComponent]
    public sealed class WorldGuideInstructionView : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private WorldGuideBindingKind bindingKind = WorldGuideBindingKind.None;
        [SerializeField] private GameObject wasdDisplayRoot;
        [SerializeField] private GameObject arrowDisplayRoot;
        [SerializeField] private TMP_Text actionKeyLabel;

        public WorldGuideBindingKind BindingKind => bindingKind;

        public void ApplyKeyboardBindings(KeyboardBindingSettingsSnapshot snapshot)
        {
            switch (bindingKind)
            {
                case WorldGuideBindingKind.Movement:
                    SetMovementScheme(snapshot.MovementScheme);
                    break;
                case WorldGuideBindingKind.Push:
                    SetActionKey(snapshot.PushDisplayName);
                    break;
                case WorldGuideBindingKind.Flip:
                    SetActionKey(snapshot.FlipDisplayName);
                    break;
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
            if (bindingKind == WorldGuideBindingKind.Movement &&
                wasdDisplayRoot == null &&
                arrowDisplayRoot == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"{nameof(WorldGuideInstructionView)} on '{name}' has Movement binding but no movement display roots.",
                    this);
            }

            if ((bindingKind == WorldGuideBindingKind.Push || bindingKind == WorldGuideBindingKind.Flip) &&
                actionKeyLabel == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"{nameof(WorldGuideInstructionView)} on '{name}' has {bindingKind} binding but no action key label.",
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
