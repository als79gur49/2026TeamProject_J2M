using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class TopologyBeltView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private RectTransform _faceChipContainer;
        [SerializeField] private FaceChipView[] _faceChips;
        [SerializeField] private RectTransform _activeMarker;
        [SerializeField] private TMP_Text _transitionLabel;
        [SerializeField] private Image _progressBar;

        private readonly List<FaceChipView> _runtimeChips = new List<FaceChipView>();
        private TopologyBeltViewModel _viewModel;
        private Tween _markerTween;

        public TopologyBeltViewModel ViewModel => _viewModel;

        public IReadOnlyList<FaceChipView> FaceChips
        {
            get
            {
                EnsureBuilt();
                return _runtimeChips;
            }
        }

        public void Bind(TopologyBeltViewModel viewModel)
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
            KillMarkerTween();
        }

        private void OnDestroy()
        {
            KillMarkerTween();
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
                layout.childAlignment = TextAnchor.UpperRight;
                layout.spacing = 4.0f;
                layout.childControlHeight = false;
                layout.childControlWidth = false;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = false;
            }

            if (_labelText == null)
            {
                _labelText = CreateText("LabelText", "SURFACE", 16, TextAlignmentOptions.Right);
            }

            if (_faceChipContainer == null)
            {
                var container = new GameObject("FaceChipContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                container.transform.SetParent(transform, false);
                _faceChipContainer = (RectTransform)container.transform;
                var layout = container.GetComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleRight;
                layout.spacing = 4.0f;
                layout.childControlHeight = false;
                layout.childControlWidth = false;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = false;
            }

            if (_transitionLabel == null)
            {
                _transitionLabel = CreateText("TransitionLabel", string.Empty, 13, TextAlignmentOptions.Right);
            }

            _runtimeChips.Clear();
            if (_faceChips != null)
            {
                for (var i = 0; i < _faceChips.Length; i++)
                {
                    if (_faceChips[i] != null)
                    {
                        _runtimeChips.Add(_faceChips[i]);
                    }
                }
            }

            while (_runtimeChips.Count < TopologyBeltViewModel.CanonicalFaceLabels.Length)
            {
                _runtimeChips.Add(CreateChip(_runtimeChips.Count));
            }
        }

        private TMP_Text CreateText(
            string childName,
            string text,
            int fontSize,
            TextAlignmentOptions alignment)
        {
            var child = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(transform, false);
            var label = child.GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private FaceChipView CreateChip(int index)
        {
            var chip = new GameObject(
                $"FaceChipView {TopologyBeltViewModel.CanonicalFaceLabels[index]}",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(FaceChipView));
            chip.transform.SetParent(_faceChipContainer, false);
            var rect = (RectTransform)chip.transform;
            rect.sizeDelta = new Vector2(72.0f, 28.0f);
            return chip.GetComponent<FaceChipView>();
        }

        private void RefreshView()
        {
            EnsureBuilt();
            if (_root != null)
            {
                _root.SetActive(_viewModel != null);
            }

            if (_viewModel == null)
            {
                KillMarkerTween();
                return;
            }

            if (_labelText != null)
            {
                _labelText.text = "SURFACE";
            }

            for (var i = 0; i < _runtimeChips.Count; i++)
            {
                _runtimeChips[i].gameObject.SetActive(i < _viewModel.Chips.Length);
                if (i >= _viewModel.Chips.Length)
                {
                    continue;
                }

                var pulse = _viewModel.AnimationHint.PulseDestination &&
                    _viewModel.AnimationHint.DestinationFaceIndex == i;
                _runtimeChips[i].Bind(_viewModel.Chips[i], pulse, HudAnimationSettings.Default);
            }

            if (_transitionLabel != null)
            {
                _transitionLabel.text = _viewModel.IsTransitionActive
                    ? _viewModel.TransitionLabel
                    : _viewModel.CurrentFaceLabel;
            }

            if (_progressBar != null)
            {
                _progressBar.gameObject.SetActive(_viewModel.IsTransitionActive);
                _progressBar.fillAmount = _viewModel.Progress01;
            }

            MoveMarker();
        }

        private void MoveMarker()
        {
            if (_activeMarker == null ||
                _viewModel == null ||
                _viewModel.CurrentFaceIndex < 0 ||
                _viewModel.CurrentFaceIndex >= _runtimeChips.Count)
            {
                return;
            }

            var target = (RectTransform)_runtimeChips[_viewModel.CurrentFaceIndex].transform;
            KillMarkerTween();
            _markerTween = _activeMarker
                .DOMove(target.position, 0.18f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void KillMarkerTween()
        {
            if (_markerTween == null)
            {
                return;
            }

            _markerTween.Kill(false);
            _markerTween = null;
        }
    }
}
