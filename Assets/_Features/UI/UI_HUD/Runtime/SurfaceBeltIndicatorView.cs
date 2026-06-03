using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class SurfaceBeltIndicatorView : MonoBehaviour
    {
        private const string AllIn1UiMaskShaderName = "AllIn1SpriteShader/AllIn1SpriteShaderUiMask";
        private const string ShineKeyword = "SHINE_ON";
        private static readonly int ShineColorId = Shader.PropertyToID("_ShineColor");
        private static readonly int ShineLocationId = Shader.PropertyToID("_ShineLocation");
        private static readonly int ShineRotateId = Shader.PropertyToID("_ShineRotate");
        private static readonly int ShineWidthId = Shader.PropertyToID("_ShineWidth");
        private static readonly int ShineGlowId = Shader.PropertyToID("_ShineGlow");

        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _maskRoot;
        [SerializeField] private RectTransform _beltContent;
        [SerializeField] private SurfaceBeltCellView[] _cells;
        [SerializeField] private RectTransform _centerArrow;
        [SerializeField] private SurfaceBeltStyleProfile _styleProfile;
        [SerializeField] private SurfaceBeltButtonBadgeStyleProfile _buttonBadgeStyleProfile;
        [SerializeField] private float _animationDurationSeconds = 0.18f;
        [SerializeField] private Ease _animationEase = Ease.OutQuad;
        [SerializeField] private float _centerArrowNudgePixels = 8.0f;
        [SerializeField] private float _centerArrowAnimationDurationSeconds = 0.16f;
        [SerializeField] private Ease _centerArrowEase = Ease.OutQuad;
        [SerializeField] private bool _centerArrowShineEnabled = true;
        [SerializeField] private float _centerArrowShineDurationSeconds = 0.2f;
        [SerializeField] private float _centerArrowShineWidth = 0.14f;
        [SerializeField] private float _centerArrowShineGlow = 2.4f;
        [SerializeField] private float _centerArrowShineRotateRadians = 0.0f;
        [SerializeField] private Color _centerArrowShineColor = Color.white;
        [SerializeField] private bool _useUnscaledTime = true;

        private SurfaceBeltViewModel _viewModel;
        private Tween _moveTween;
        private Tween _centerArrowTween;
        private Tween _centerArrowShineTween;
        private Image _centerArrowImage;
        private Material _centerArrowOriginalMaterial;
        private Material _centerArrowMaterialInstance;
        private Vector2 _centerArrowBaseAnchoredPosition;
        private bool _hasCenterArrowBaseAnchoredPosition;
        private int _lastAnimatedSequenceId;

        public SurfaceBeltViewModel ViewModel => _viewModel;

        public RectTransform BeltContent => _beltContent;

        public SurfaceBeltCellView[] Cells => _cells;

        public SurfaceBeltStyleProfile StyleProfile => _styleProfile;

        public SurfaceBeltButtonBadgeStyleProfile ButtonBadgeStyleProfile => _buttonBadgeStyleProfile;

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
            RequireReference(_buttonBadgeStyleProfile, nameof(_buttonBadgeStyleProfile));

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

            if (!_buttonBadgeStyleProfile.TryValidate(out validationMessage))
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
            KillCenterArrowTween(true);
            KillCenterArrowShineTween(true);
        }

        private void OnDestroy()
        {
            KillMoveTween();
            KillCenterArrowTween(true);
            KillCenterArrowShineTween(true);
            DisposeCenterArrowMaterialInstance();
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
                KillCenterArrowTween(true);
                KillCenterArrowShineTween(true);
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
                KillCenterArrowTween(true);
                KillCenterArrowShineTween(true);
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
            var authoredCellStepHeight = GetAuthoredCellStepHeight();
            var targetY = viewModel.Direction == SurfaceBeltDirection.Forward
                ? -authoredCellStepHeight
                : authoredCellStepHeight;

            PlayCenterArrowFeedback(viewModel.Direction);
            PlayCenterArrowShine(viewModel.Direction);

            _moveTween = _beltContent
                .DOAnchorPosY(targetY, Mathf.Max(0.0f, _animationDurationSeconds))
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

        private void PlayCenterArrowFeedback(SurfaceBeltDirection direction)
        {
            if (_centerArrow == null || direction == SurfaceBeltDirection.None)
            {
                return;
            }

            CacheCenterArrowBasePosition();
            KillCenterArrowTween(true);

            var nudge = direction == SurfaceBeltDirection.Forward
                ? -Mathf.Abs(_centerArrowNudgePixels)
                : Mathf.Abs(_centerArrowNudgePixels);
            if (Mathf.Approximately(nudge, 0.0f))
            {
                return;
            }

            var duration = Mathf.Max(0.0f, _centerArrowAnimationDurationSeconds);
            var target = _centerArrowBaseAnchoredPosition + new Vector2(0.0f, nudge);
            _centerArrowTween = DOTween.Sequence()
                .Append(_centerArrow.DOAnchorPos(target, duration * 0.45f).SetEase(_centerArrowEase))
                .Append(_centerArrow.DOAnchorPos(_centerArrowBaseAnchoredPosition, duration * 0.55f).SetEase(_centerArrowEase))
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnKill(() => _centerArrowTween = null);
        }

        private void PlayCenterArrowShine(SurfaceBeltDirection direction)
        {
            if (!_centerArrowShineEnabled ||
                direction == SurfaceBeltDirection.None ||
                !TryEnsureCenterArrowShineMaterial(out var material))
            {
                return;
            }

            KillCenterArrowShineTween(true);

            var from = direction == SurfaceBeltDirection.Forward ? 1.0f : 0.0f;
            var to = direction == SurfaceBeltDirection.Forward ? 0.0f : 1.0f;
            var duration = Mathf.Max(0.0f, _centerArrowShineDurationSeconds);
            material.SetColor(ShineColorId, _centerArrowShineColor);
            material.SetFloat(ShineRotateId, _centerArrowShineRotateRadians);
            material.SetFloat(ShineWidthId, Mathf.Max(0.05f, _centerArrowShineWidth));
            material.SetFloat(ShineGlowId, 0.0f);
            material.SetFloat(ShineLocationId, from);

            _centerArrowShineTween = DOTween.Sequence()
                .Append(DOTween.To(
                    () => from,
                    value => material.SetFloat(ShineLocationId, value),
                    to,
                    duration))
                .Join(DOTween.Sequence()
                    .Append(DOTween.To(
                        () => 0.0f,
                        value => material.SetFloat(ShineGlowId, value),
                        Mathf.Max(0.0f, _centerArrowShineGlow),
                        duration * 0.35f))
                    .Append(DOTween.To(
                        () => Mathf.Max(0.0f, _centerArrowShineGlow),
                        value => material.SetFloat(ShineGlowId, value),
                        0.0f,
                        duration * 0.65f)))
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnKill(() =>
                {
                    ResetCenterArrowShine();
                    _centerArrowShineTween = null;
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
                _cells[i].Bind(
                    cells[i],
                    _styleProfile,
                    _viewModel.GetButtonRemainderForSlot(cells[i].SlotIndex),
                    _buttonBadgeStyleProfile);
            }
        }

        private float GetAuthoredCellStepHeight()
        {
            if (_cells == null || _cells.Length < 5)
            {
                throw new InvalidOperationException(
                    $"{nameof(SurfaceBeltIndicatorView)} requires authored neighboring cells to resolve transition distance.");
            }

            var center = (RectTransform)_cells[3].transform;
            var next = (RectTransform)_cells[4].transform;
            var step = Mathf.Abs(next.anchoredPosition.y - center.anchoredPosition.y);
            if (step <= 0.0f)
            {
                throw new InvalidOperationException(
                    $"{nameof(SurfaceBeltIndicatorView)} requires authored cells to have a positive vertical step.");
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

        private void KillCenterArrowTween(bool restorePosition)
        {
            if (_centerArrowTween != null)
            {
                _centerArrowTween.Kill(false);
                _centerArrowTween = null;
            }

            if (restorePosition)
            {
                ResetCenterArrowPosition();
            }
        }

        private void CacheCenterArrowBasePosition()
        {
            if (_centerArrow == null || _hasCenterArrowBaseAnchoredPosition)
            {
                return;
            }

            _centerArrowBaseAnchoredPosition = _centerArrow.anchoredPosition;
            _hasCenterArrowBaseAnchoredPosition = true;
        }

        private void ResetCenterArrowPosition()
        {
            if (_centerArrow == null || !_hasCenterArrowBaseAnchoredPosition)
            {
                return;
            }

            _centerArrow.anchoredPosition = _centerArrowBaseAnchoredPosition;
        }

        private void KillCenterArrowShineTween(bool resetShine)
        {
            if (_centerArrowShineTween != null)
            {
                _centerArrowShineTween.Kill(false);
                _centerArrowShineTween = null;
            }

            if (resetShine)
            {
                ResetCenterArrowShine();
            }
        }

        private bool TryEnsureCenterArrowShineMaterial(out Material material)
        {
            material = null;
            if (_centerArrow == null)
            {
                return false;
            }

            _centerArrowImage ??= _centerArrow.GetComponent<Image>();
            if (_centerArrowImage == null)
            {
                return false;
            }

            if (_centerArrowMaterialInstance != null)
            {
                material = _centerArrowMaterialInstance;
                return true;
            }

            var shader = Shader.Find(AllIn1UiMaskShaderName);
            if (shader == null)
            {
                return false;
            }

            _centerArrowOriginalMaterial = _centerArrowImage.material;
            _centerArrowMaterialInstance = _centerArrowOriginalMaterial != null &&
                                           _centerArrowOriginalMaterial.shader == shader
                ? new Material(_centerArrowOriginalMaterial)
                : new Material(shader);
            _centerArrowMaterialInstance.name = $"{_centerArrow.name}_AllIn1Shine_Runtime";
            _centerArrowMaterialInstance.EnableKeyword(ShineKeyword);
            _centerArrowMaterialInstance.SetFloat(ShineGlowId, 0.0f);
            _centerArrowMaterialInstance.SetFloat(ShineWidthId, Mathf.Max(0.05f, _centerArrowShineWidth));
            _centerArrowMaterialInstance.SetColor(ShineColorId, _centerArrowShineColor);
            _centerArrowImage.material = _centerArrowMaterialInstance;
            material = _centerArrowMaterialInstance;
            return true;
        }

        private void ResetCenterArrowShine()
        {
            if (_centerArrowMaterialInstance == null)
            {
                return;
            }

            _centerArrowMaterialInstance.SetFloat(ShineGlowId, 0.0f);
        }

        private void DisposeCenterArrowMaterialInstance()
        {
            if (_centerArrowImage != null)
            {
                _centerArrowImage.material = _centerArrowOriginalMaterial;
            }

            if (_centerArrowMaterialInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_centerArrowMaterialInstance);
            }
            else
            {
                DestroyImmediate(_centerArrowMaterialInstance);
            }

            _centerArrowMaterialInstance = null;
            _centerArrowOriginalMaterial = null;
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
