using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class TopologyBeltView : MonoBehaviour
    {
        private const int AuthoredFaceChipCount = 6;

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
                ValidateAuthoredStructureOrThrow();
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

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_root, nameof(_root));
            RequireReference(_labelText, nameof(_labelText));
            RequireReference(_faceChipContainer, nameof(_faceChipContainer));
            RequireReference(_transitionLabel, nameof(_transitionLabel));
            if (_faceChips == null || _faceChips.Length != AuthoredFaceChipCount)
            {
                throw new InvalidOperationException($"{nameof(TopologyBeltView)} requires exactly {AuthoredFaceChipCount} authored face chip references.");
            }

            _runtimeChips.Clear();
            for (var i = 0; i < _faceChips.Length; i++)
            {
                var chip = _faceChips[i];
                if (chip == null)
                {
                    throw new InvalidOperationException($"{nameof(TopologyBeltView)} has a null authored face chip at index {i}.");
                }

                chip.ValidateAuthoredStructureOrThrow();
                _runtimeChips.Add(chip);
            }
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

        private void RefreshView()
        {
            ValidateAuthoredStructureOrThrow();
            if (_root != null)
            {
                _root.SetActive(_viewModel != null);
            }

            if (_viewModel == null)
            {
                KillMarkerTween();
                return;
            }

            if (_viewModel.Chips.Length > _runtimeChips.Count)
            {
                throw new InvalidOperationException(
                    $"{nameof(TopologyBeltView)} has {_runtimeChips.Count} authored face chips but received {_viewModel.Chips.Length} chip view models.");
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

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(TopologyBeltView)} is missing authored reference '{fieldName}'.");
            }
        }

    }
}
