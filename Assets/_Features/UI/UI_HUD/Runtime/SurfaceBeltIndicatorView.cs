using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class SurfaceBeltIndicatorView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _maskRoot;
        [SerializeField] private RectTransform _beltContent;
        [SerializeField] private SurfaceBeltCellView[] _cells;
        [SerializeField] private RectTransform _centerArrow;
        [SerializeField] private SurfaceBeltStyleProfile _styleProfile;
        [SerializeField] private float _animationDurationSeconds = 0.18f;
        [SerializeField] private Ease _animationEase = Ease.OutQuad;
        [SerializeField] private bool _useUnscaledTime = true;

        private SurfaceBeltViewModel _viewModel;
        private Tween _moveTween;
        private int _lastAnimatedSequenceId;

        public SurfaceBeltViewModel ViewModel => _viewModel;

        public RectTransform BeltContent => _beltContent;

        public SurfaceBeltCellView[] Cells => _cells;

        public SurfaceBeltStyleProfile StyleProfile => _styleProfile;

        public void Bind(SurfaceBeltViewModel viewModel)
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
            RequireReference(_maskRoot, nameof(_maskRoot));
            RequireReference(_beltContent, nameof(_beltContent));
            RequireReference(_centerArrow, nameof(_centerArrow));
            RequireReference(_styleProfile, nameof(_styleProfile));

            if (_maskRoot.GetComponent<RectMask2D>() == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceBeltIndicatorView)} requires RectMask2D on {_maskRoot.name}.");
            }

            if (_cells == null || _cells.Length != SurfaceBeltViewModel.AuthoredCellCount)
            {
                throw new InvalidOperationException(
                    $"{nameof(SurfaceBeltIndicatorView)} requires exactly {SurfaceBeltViewModel.AuthoredCellCount} authored cells.");
            }

            if (!_styleProfile.TryValidate(out var validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            for (var i = 0; i < _cells.Length; i++)
            {
                RequireReference(_cells[i], $"{nameof(_cells)}[{i}]");
                _cells[i].ValidateAuthoredStructureOrThrow();
                if (!_cells[i].transform.IsChildOf(_beltContent))
                {
                    throw new InvalidOperationException($"{_cells[i].name} must be under {_beltContent.name}.");
                }
            }

            if (_centerArrow.transform.IsChildOf(_beltContent))
            {
                throw new InvalidOperationException($"{nameof(_centerArrow)} must stay outside {_beltContent.name}.");
            }
        }

        private void OnEnable()
        {
            RefreshView();
        }

        private void OnDisable()
        {
            KillMoveTween();
        }

        private void OnDestroy()
        {
            KillMoveTween();
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
                _root.SetActive(_viewModel != null && _viewModel.Visible);
            }

            if (_viewModel == null || !_viewModel.Visible)
            {
                KillMoveTween();
                return;
            }

            if (ShouldStartTransition(_viewModel))
            {
                StartTransition(_viewModel);
                return;
            }

            if (!_viewModel.IsTransitioning)
            {
                KillMoveTween();
                ApplyCells(_viewModel.Cells);
                ResetContentPosition();
            }
        }

        private bool ShouldStartTransition(SurfaceBeltViewModel viewModel)
        {
            return viewModel.IsTransitioning &&
                   viewModel.Direction != SurfaceBeltDirection.None &&
                   viewModel.TransitionSequenceId > 0 &&
                   viewModel.TransitionSequenceId != _lastAnimatedSequenceId;
        }

        private void StartTransition(SurfaceBeltViewModel viewModel)
        {
            KillMoveTween();
            _lastAnimatedSequenceId = viewModel.TransitionSequenceId;
            ApplyCells(viewModel.Cells);
            ResetContentPosition();

            var destinationSlotIndex = viewModel.DestinationSlotIndex;
            var authoredCellStepWidth = GetAuthoredCellStepWidth();
            var targetX = viewModel.Direction == SurfaceBeltDirection.Forward
                ? -authoredCellStepWidth
                : authoredCellStepWidth;

            _moveTween = _beltContent
                .DOAnchorPosX(targetX, Mathf.Max(0.0f, _animationDurationSeconds))
                .SetEase(_animationEase)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    ApplyCells(SurfaceBeltSlotMapping.BuildCells(destinationSlotIndex));
                    ResetContentPosition();
                    _moveTween = null;
                });
        }

        private void ApplyCells(SurfaceBeltCellViewModel[] cells)
        {
            if (cells == null || cells.Length != _cells.Length)
            {
                throw new InvalidOperationException($"{nameof(SurfaceBeltIndicatorView)} received an invalid cell set.");
            }

            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i].Bind(cells[i], _styleProfile);
            }
        }

        private float GetAuthoredCellStepWidth()
        {
            if (_cells == null || _cells.Length < 5)
            {
                throw new InvalidOperationException(
                    $"{nameof(SurfaceBeltIndicatorView)} requires authored neighboring cells to resolve transition distance.");
            }

            var center = (RectTransform)_cells[3].transform;
            var next = (RectTransform)_cells[4].transform;
            var step = Mathf.Abs(next.anchoredPosition.x - center.anchoredPosition.x);
            if (step <= 0.0f)
            {
                throw new InvalidOperationException(
                    $"{nameof(SurfaceBeltIndicatorView)} requires authored cells to have a positive horizontal step.");
            }

            return step;
        }

        private void ResetContentPosition()
        {
            if (_beltContent != null)
            {
                _beltContent.anchoredPosition = Vector2.zero;
            }
        }

        private void KillMoveTween()
        {
            if (_moveTween == null)
            {
                return;
            }

            _moveTween.Kill(false);
            _moveTween = null;
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceBeltIndicatorView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
