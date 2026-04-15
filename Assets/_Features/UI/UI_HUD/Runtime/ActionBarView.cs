using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class ActionBarView : MonoBehaviour
    {
        private const float TransientFeedbackDurationSeconds = 0.12f;

        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _primaryLabel;
        [SerializeField] private Text _primaryStateLabel;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private Text _secondaryLabel;
        [SerializeField] private Text _secondaryStateLabel;
        [SerializeField] private Button _secondaryButton;
        [SerializeField] private Text _outcomeLabel;
        [SerializeField] private Text _feedbackLabel;

        private readonly Dictionary<HudActionSlotId, Coroutine> _transientCoroutines = new();
        private readonly Dictionary<HudActionSlotId, Color> _transientOverrideColors = new();
        private readonly Dictionary<HudActionSlotId, int> _transientFeedbackRevisions = new();
        private ActionBarViewModel _viewModel;

        public event Action<HudActionSlotId> SlotRequested;

        public ActionBarViewModel ViewModel => _viewModel;

        public void Bind(ActionBarViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            _viewModel = viewModel;
            if (_viewModel == null)
            {
                ClearTransientFeedback();
            }

            if (_viewModel != null)
            {
                _viewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

        public void ClickSlot(HudActionSlotId slotId)
        {
            if (_viewModel == null)
            {
                return;
            }

            var slot = TryGetSlot(slotId, out var slotViewModel)
                ? slotViewModel
                : default;

            if (slot.SlotId != slotId || !slot.IsInteractive)
            {
                return;
            }

            SlotRequested?.Invoke(slotId);
        }

        public void ClickPrimary()
        {
            ClickSlot(HudActionSlotId.Primary);
        }

        public void ClickSecondary()
        {
            ClickSlot(HudActionSlotId.Secondary);
        }

        private void OnEnable()
        {
            if (_primaryButton != null)
            {
                _primaryButton.onClick.RemoveListener(ClickPrimary);
                _primaryButton.onClick.AddListener(ClickPrimary);
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.onClick.RemoveListener(ClickSecondary);
                _secondaryButton.onClick.AddListener(ClickSecondary);
            }

            RefreshView();
        }

        private void OnDisable()
        {
            if (_primaryButton != null)
            {
                _primaryButton.onClick.RemoveListener(ClickPrimary);
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.onClick.RemoveListener(ClickSecondary);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_primaryLabel, nameof(_primaryLabel));
            ValidateSerializedReference(_primaryStateLabel, nameof(_primaryStateLabel));
            ValidateSerializedReference(_primaryButton, nameof(_primaryButton));
            ValidateSerializedReference(_secondaryLabel, nameof(_secondaryLabel));
            ValidateSerializedReference(_secondaryStateLabel, nameof(_secondaryStateLabel));
            ValidateSerializedReference(_secondaryButton, nameof(_secondaryButton));
            ValidateSerializedReference(_outcomeLabel, nameof(_outcomeLabel));
            ValidateSerializedReference(_feedbackLabel, nameof(_feedbackLabel));
        }
#endif

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            if (_primaryButton != null)
            {
                _primaryButton.onClick.RemoveListener(ClickPrimary);
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.onClick.RemoveListener(ClickSecondary);
            }

            ClearTransientFeedback();
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = "Action Bar";
            }

            if (_viewModel == null)
            {
                return;
            }

            ApplySlot(HudActionSlotId.Primary, _primaryLabel, _primaryStateLabel, _primaryButton);
            ApplySlot(HudActionSlotId.Secondary, _secondaryLabel, _secondaryStateLabel, _secondaryButton);

            if (_outcomeLabel != null)
            {
                _outcomeLabel.text = string.IsNullOrEmpty(_viewModel.OutcomeText)
                    ? "Outcome: -"
                    : $"Outcome: {_viewModel.OutcomeText}";
            }

            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = string.IsNullOrEmpty(_viewModel.FeedbackText)
                    ? string.Empty
                    : $"Feedback: {_viewModel.FeedbackText}";
            }
        }

        private void ApplySlot(
            HudActionSlotId slotId,
            Text label,
            Text stateLabel,
            Button button)
        {
            var hasSlot = TryGetSlot(slotId, out var slotViewModel);
            if (label != null)
            {
                label.text = hasSlot ? slotViewModel.LabelText : string.Empty;
            }

            if (stateLabel != null)
            {
                stateLabel.text = hasSlot ? slotViewModel.StateText : string.Empty;
            }

            if (button != null)
            {
                button.interactable = hasSlot && slotViewModel.IsInteractive;

                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    if (hasSlot)
                    {
                        TriggerTransientFeedback(slotId, slotViewModel);
                    }

                    image.color = ResolveSlotColor(slotId, hasSlot && slotViewModel.IsHighlighted);
                }
            }
        }

        private void TriggerTransientFeedback(HudActionSlotId slotId, ActionSlotViewModel slotViewModel)
        {
            if (slotViewModel.TransientFeedbackKind == ActionSlotTransientFeedbackKind.None)
            {
                return;
            }

            if (_transientFeedbackRevisions.TryGetValue(slotId, out var lastRevision) &&
                lastRevision == slotViewModel.TransientFeedbackRevision)
            {
                return;
            }

            _transientFeedbackRevisions[slotId] = slotViewModel.TransientFeedbackRevision;
            if (!Application.isPlaying)
            {
                return;
            }

            if (_transientCoroutines.TryGetValue(slotId, out var activeCoroutine) && activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
            }

            _transientOverrideColors[slotId] = GetTransientFeedbackColor(slotViewModel.TransientFeedbackKind);
            _transientCoroutines[slotId] = StartCoroutine(PlayTransientFeedback(slotId));
        }

        private IEnumerator PlayTransientFeedback(HudActionSlotId slotId)
        {
            RefreshView();
            yield return new WaitForSecondsRealtime(TransientFeedbackDurationSeconds);
            _transientOverrideColors.Remove(slotId);
            _transientCoroutines.Remove(slotId);
            RefreshView();
        }

        private void ClearTransientFeedback()
        {
            foreach (var pair in _transientCoroutines)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            _transientCoroutines.Clear();
            _transientOverrideColors.Clear();
            _transientFeedbackRevisions.Clear();
        }

        private Color ResolveSlotColor(HudActionSlotId slotId, bool isHighlighted)
        {
            if (_transientOverrideColors.TryGetValue(slotId, out var transientColor))
            {
                return transientColor;
            }

            return isHighlighted
                ? new Color(0.32f, 0.42f, 0.22f, 1f)
                : new Color(0.20f, 0.25f, 0.34f, 1f);
        }

        private static Color GetTransientFeedbackColor(ActionSlotTransientFeedbackKind feedbackKind)
        {
            switch (feedbackKind)
            {
                case ActionSlotTransientFeedbackKind.Started:
                    return new Color(0.80f, 0.70f, 0.28f, 1f);
                case ActionSlotTransientFeedbackKind.Resolved:
                    return new Color(0.28f, 0.62f, 0.70f, 1f);
                case ActionSlotTransientFeedbackKind.Completed:
                    return new Color(0.34f, 0.52f, 0.28f, 1f);
                case ActionSlotTransientFeedbackKind.Canceled:
                    return new Color(0.64f, 0.30f, 0.24f, 1f);
                default:
                    return new Color(0.20f, 0.25f, 0.34f, 1f);
            }
        }

        private bool TryGetSlot(HudActionSlotId slotId, out ActionSlotViewModel slotViewModel)
        {
            var slots = _viewModel?.Slots;
            if (slots != null)
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    if (slots[i].SlotId == slotId)
                    {
                        slotViewModel = slots[i];
                        return true;
                    }
                }
            }

            slotViewModel = default;
            return false;
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(ActionBarView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
