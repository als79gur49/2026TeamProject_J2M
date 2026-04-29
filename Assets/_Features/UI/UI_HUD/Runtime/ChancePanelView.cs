using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
                EnsureBuilt();
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

        private void EnsureBuilt()
        {
            if (_root == null)
            {
                _root = gameObject;
            }

            if (GetComponent<LayoutGroup>() == null)
            {
                var layout = gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.spacing = 4.0f;
                layout.childControlHeight = false;
                layout.childControlWidth = false;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = false;
            }

            var rect = transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            if (_labelText == null)
            {
                _labelText = CreateText("LabelText", "CHANCES", 18);
                _labelText.alignment = TextAlignmentOptions.Left;
            }

            if (_slotContainer == null)
            {
                var container = new GameObject("SlotContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                container.transform.SetParent(transform, false);
                _slotContainer = (RectTransform)container.transform;
                var layout = container.GetComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.spacing = 6.0f;
                layout.childControlHeight = false;
                layout.childControlWidth = false;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = false;
            }

            if (_floatingFeedbackRoot == null)
            {
                var floating = new GameObject("FloatingFeedbackRoot", typeof(RectTransform));
                floating.transform.SetParent(transform, false);
                _floatingFeedbackRoot = (RectTransform)floating.transform;
            }

            _runtimeSlots.Clear();
            if (_slotViews != null)
            {
                for (var i = 0; i < _slotViews.Length; i++)
                {
                    if (_slotViews[i] != null)
                    {
                        _runtimeSlots.Add(_slotViews[i]);
                    }
                }
            }

            while (_runtimeSlots.Count < AuthoredSlotCount)
            {
                _runtimeSlots.Add(CreateSlot(_runtimeSlots.Count));
            }
        }

        private TMP_Text CreateText(
            string childName,
            string text,
            int fontSize)
        {
            var child = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(transform, false);
            var label = child.GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.raycastTarget = false;
            return label;
        }

        private ChanceSlotView CreateSlot(int index)
        {
            var slot = new GameObject($"ChanceSlotView {index}", typeof(RectTransform), typeof(ChanceSlotView));
            slot.transform.SetParent(_slotContainer, false);
            var rect = (RectTransform)slot.transform;
            rect.sizeDelta = new Vector2(26.0f, 26.0f);
            return slot.GetComponent<ChanceSlotView>();
        }

        private void RefreshView()
        {
            EnsureBuilt();
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

            if (_labelText != null)
            {
                _labelText.text = "CHANCES";
                _labelText.color = _viewModel.IsLastChance
                    ? new Color(1.0f, 0.38f, 0.42f, 1.0f)
                    : Color.white;
            }

            EnsureSlotCapacity(_viewModel.MaxChances);
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

        private void EnsureSlotCapacity(int targetCount)
        {
            while (_runtimeSlots.Count < targetCount)
            {
                _runtimeSlots.Add(CreateSlot(_runtimeSlots.Count));
            }
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
    }
}
