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
        private const float PanelWidth = 220.0f;
        private const float LabelHeight = 20.0f;
        private const float SlotContainerHeight = 28.0f;

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

            ConfigureLayoutElement((RectTransform)_labelText.transform, PanelWidth, LabelHeight);

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

            ConfigureLayoutElement(_slotContainer, PanelWidth, SlotContainerHeight);

            if (_floatingFeedbackRoot == null)
            {
                var floating = new GameObject("FloatingFeedbackRoot", typeof(RectTransform));
                floating.transform.SetParent(transform, false);
                _floatingFeedbackRoot = (RectTransform)floating.transform;
            }

            StretchOverlay(_floatingFeedbackRoot);
            ConfigureIgnoredLayout(_floatingFeedbackRoot);

            RebuildRuntimeSlotCache();

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

        private static void ConfigureLayoutElement(RectTransform rect, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            rect.sizeDelta = new Vector2(width, height);
            var layoutElement = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = false;
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
        }

        private static void ConfigureIgnoredLayout(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            var layoutElement = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
        }

        private static void StretchOverlay(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            TrimSlotCapacity(_viewModel.MaxChances);
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

        private void RebuildRuntimeSlotCache()
        {
            _runtimeSlots.Clear();
            if (_slotViews != null)
            {
                for (var i = 0; i < _slotViews.Length; i++)
                {
                    AddRuntimeSlot(_slotViews[i]);
                }
            }

            if (_slotContainer == null)
            {
                return;
            }

            var discoveredSlots = _slotContainer.GetComponentsInChildren<ChanceSlotView>(true);
            for (var i = 0; i < discoveredSlots.Length; i++)
            {
                AddRuntimeSlot(discoveredSlots[i]);
            }
        }

        private void AddRuntimeSlot(ChanceSlotView slot)
        {
            if (slot == null)
            {
                return;
            }

            for (var i = 0; i < _runtimeSlots.Count; i++)
            {
                if (_runtimeSlots[i] == slot ||
                    (_runtimeSlots[i] != null && slot != null && _runtimeSlots[i].GetInstanceID() == slot.GetInstanceID()))
                {
                    return;
                }
            }

            _runtimeSlots.Add(slot);
        }

        private void EnsureSlotCapacity(int targetCount)
        {
            while (_runtimeSlots.Count < targetCount)
            {
                _runtimeSlots.Add(CreateSlot(_runtimeSlots.Count));
            }
        }

        private void TrimSlotCapacity(int targetCount)
        {
            for (var i = _runtimeSlots.Count - 1; i >= targetCount; i--)
            {
                var slot = _runtimeSlots[i];
                _runtimeSlots.RemoveAt(i);
                if (slot == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(slot.gameObject);
                }
                else
                {
                    DestroyImmediate(slot.gameObject);
                }
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
