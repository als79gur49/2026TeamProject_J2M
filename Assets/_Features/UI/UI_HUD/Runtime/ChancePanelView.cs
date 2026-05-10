using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    public sealed class ChancePanelView : MonoBehaviour
    {
        private const int AuthoredSlotCount = 3;

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private RectTransform _slotContainer;
        [SerializeField] private ChanceSlotView[] _slotViews;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private RectTransform _floatingFeedbackRoot;

        private readonly List<ChanceSlotView> _runtimeSlots = new List<ChanceSlotView>();
        private ChancePanelViewModel _viewModel;
        private Sequence _panelSequence;
        private int _lastAnimationSequenceId;

        public ChancePanelViewModel ViewModel => _viewModel;

        public IReadOnlyList<ChanceSlotView> SlotViews
        {
            get
            {
                ValidateAuthoredStructureOrThrow();
                return _runtimeSlots;
            }
        }

        public void Bind(ChancePanelViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_root, nameof(_root));
            RequireReference(_labelText, nameof(_labelText));
            RequireReference(_slotContainer, nameof(_slotContainer));
            RequireReference(_floatingFeedbackRoot, nameof(_floatingFeedbackRoot));
            if (_slotViews == null || _slotViews.Length != AuthoredSlotCount)
            {
                throw new InvalidOperationException($"{nameof(ChancePanelView)} requires exactly {AuthoredSlotCount} authored slot references.");
            }

            _runtimeSlots.Clear();
            for (var i = 0; i < _slotViews.Length; i++)
            {
                var slot = _slotViews[i];
                if (slot == null)
                {
                    throw new InvalidOperationException($"{nameof(ChancePanelView)} has a null authored slot at index {i}.");
                }

                if (!slot.transform.IsChildOf(_slotContainer))
                {
                    throw new InvalidOperationException($"{nameof(ChancePanelView)} slot {i} must be under SlotContainer.");
                }

                slot.ValidateAuthoredStructureOrThrow();
                _runtimeSlots.Add(slot);
            }
        }

        private void OnEnable()
        {
            RefreshView();
        }

        private void OnDisable()
        {
            KillPanelSequence();
        }

        private void OnDestroy()
        {
            KillPanelSequence();
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            ValidateAuthoredStructureOrThrow();
            var isVisible = _viewModel != null && _viewModel.HasChances;
            if (_root != null)
            {
                _root.SetActive(isVisible);
            }

            if (!isVisible)
            {
                KillPanelSequence();
                return;
            }

            if (_viewModel.MaxChances > _runtimeSlots.Count)
            {
                throw new InvalidOperationException(
                    $"{nameof(ChancePanelView)} has {_runtimeSlots.Count} authored slots but received MaxChances {_viewModel.MaxChances}.");
            }

            if (_labelText != null)
            {
                _labelText.text = "CHANCES";
                _labelText.color = _viewModel.IsLastChance
                    ? new Color(1.0f, 0.38f, 0.42f, 1.0f)
                    : Color.white;
            }

            for (var i = 0; i < _runtimeSlots.Count; i++)
            {
                _runtimeSlots[i].gameObject.SetActive(i < _viewModel.Slots.Count);
                if (i < _viewModel.Slots.Count)
                {
                    _runtimeSlots[i].Bind(
                        _viewModel.Slots[i],
                        _viewModel.AnimationHint,
                        HudAnimationSettings.Default);
                }
            }

            if (_countText != null)
            {
                _countText.text = $"{_viewModel.RemainingChances}/{_viewModel.MaxChances}";
            }

            PlayPanelWarningIfNeeded();
        }

        private void PlayPanelWarningIfNeeded()
        {
            var hint = _viewModel.AnimationHint;
            if (hint.Kind != ChanceChangeKind.LastChanceEntered ||
                hint.SequenceId <= 0 ||
                hint.SequenceId == _lastAnimationSequenceId)
            {
                return;
            }

            _lastAnimationSequenceId = hint.SequenceId;
            KillPanelSequence();
            _panelSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            _panelSequence.Append(transform.DOPunchScale(Vector3.one * 0.08f, 0.35f, 8, 0.75f));
        }

        private void KillPanelSequence()
        {
            if (_panelSequence == null)
            {
                return;
            }

            _panelSequence.Kill(false);
            _panelSequence = null;
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(ChancePanelView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
