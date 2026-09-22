using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    [DisallowMultipleComponent]
    public sealed class MainMenuLogoEffectView : MonoBehaviour
    {
        internal const string AllIn1UiMaskShaderName = "AllIn1SpriteShader/AllIn1SpriteShaderUiMask";
        internal const string ShineKeyword = "SHINE_ON";
        internal const float AcceptedVisualTailSeconds = 0.45f;

        private const float IdleHalfCycleSeconds = 1.6f;
        private const float IdleScaleMultiplier = 1.035f;
        private const float IdleMinimumAlpha = 0.18f;
        private const float IdleMaximumAlpha = 0.26f;
        private const float ShineDurationSeconds = 0.68f;
        private const float ShineStartLocation = 0.03f;
        private const float ShineEndLocation = 0.97f;
        private const float ShinePeakGlow = 0.525f;
        private const float ShineFadeInSeconds = 0.12f;
        private const float ShineHoldSeconds = 0.38f;
        private const float ShineFadeOutSeconds = 0.18f;
        private const double SameCommandDebounceSeconds = 0.20d;
        private const int AcceptedBurstCount = 8;

        private static readonly int ShineLocationId = Shader.PropertyToID("_ShineLocation");
        private static readonly int ShineGlowId = Shader.PropertyToID("_ShineGlow");

        [SerializeField] private Image _logoImage;
        [SerializeField] private RectTransform _impactRoot;
        [SerializeField] private RectTransform _glowRoot;
        [SerializeField] private CanvasGroup _glowCanvasGroup;
        [SerializeField] private ParticleSystem _acceptedBurst;
        [SerializeField] private Material _allIn1Template;

        private Tween _idleTween;
        private Tween _shineTween;
        private Tween _impactTween;
        private Material _logoOriginalMaterial;
        private Material _logoRuntimeMaterial;
        private Vector2 _impactBasePosition;
        private Vector3 _impactBaseScale;
        private Vector3 _glowBaseScale;
        private MainMenuCommandId _lastShineCommand;
        private double _lastShineTime = double.NegativeInfinity;
        private bool _interactionBlocked;
        private bool _initialized;

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_logoImage, nameof(_logoImage));
            RequireReference(_impactRoot, nameof(_impactRoot));
            RequireReference(_glowRoot, nameof(_glowRoot));
            RequireReference(_glowCanvasGroup, nameof(_glowCanvasGroup));
            RequireReference(_acceptedBurst, nameof(_acceptedBurst));
            RequireReference(_allIn1Template, nameof(_allIn1Template));

            if (_allIn1Template.shader == null ||
                !string.Equals(_allIn1Template.shader.name, AllIn1UiMaskShaderName, StringComparison.Ordinal) ||
                !_allIn1Template.IsKeywordEnabled(ShineKeyword))
            {
                throw new InvalidOperationException(
                    $"{nameof(MainMenuLogoEffectView)} requires a '{AllIn1UiMaskShaderName}' template with '{ShineKeyword}' enabled.");
            }

            if (!_logoImage.transform.IsChildOf(_impactRoot) || _glowCanvasGroup.transform != _glowRoot)
            {
                throw new InvalidOperationException(
                    $"{nameof(MainMenuLogoEffectView)} authored hierarchy does not match its Logo impact and Glow ownership contract.");
            }
        }

        public void SetInteractionBlocked(bool blocked)
        {
            _interactionBlocked = blocked;
            if (!blocked)
            {
                return;
            }

            KillShineTween(reset: true);
            KillImpactTween(restore: true);
            StopAcceptedBurst();
        }

        public void PlayFocusShine(MainMenuLogoFocus focus)
        {
            if (_interactionBlocked || !CanPlay() || !focus.HasFocus)
            {
                return;
            }

            InitializeOrThrow();
            var now = Time.unscaledTimeAsDouble;
            if (focus.CommandId == _lastShineCommand && now - _lastShineTime < SameCommandDebounceSeconds)
            {
                return;
            }

            _lastShineCommand = focus.CommandId;
            _lastShineTime = now;
            KillShineTween(reset: true);
            ResetShine();

            var location = ShineStartLocation;
            var glow = 0f;
            _shineTween = DOTween.Sequence()
                .Append(DOTween.To(
                        () => location,
                        value =>
                        {
                            location = value;
                            _logoRuntimeMaterial.SetFloat(ShineLocationId, value);
                        },
                        ShineEndLocation,
                        ShineDurationSeconds)
                    .SetEase(Ease.Linear))
                .Join(DOTween.Sequence()
                    .Append(DOTween.To(
                        () => glow,
                        value =>
                        {
                            glow = value;
                            _logoRuntimeMaterial.SetFloat(ShineGlowId, value);
                        },
                        ShinePeakGlow,
                        ShineFadeInSeconds)
                    .SetEase(Ease.OutQuad))
                    .AppendInterval(ShineHoldSeconds)
                    .Append(DOTween.To(
                        () => glow,
                        value =>
                        {
                            glow = value;
                            _logoRuntimeMaterial.SetFloat(ShineGlowId, value);
                        },
                        0f,
                        ShineFadeOutSeconds)
                    .SetEase(Ease.InQuad)))
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    ResetShine();
                    _shineTween = null;
                })
                .OnKill(() => _shineTween = null);
        }

        public void PlayAccepted(MainMenuLogoImpactKind kind)
        {
            if (_interactionBlocked || !CanPlay())
            {
                return;
            }

            InitializeOrThrow();
            KillImpactTween(restore: true);
            RestoreImpactTransform();

            var positionSequence = DOTween.Sequence()
                .Append(_impactRoot.DOAnchorPos(_impactBasePosition + new Vector2(2f, 0f), 0.025f).SetEase(Ease.Linear))
                .Append(_impactRoot.DOAnchorPos(_impactBasePosition + new Vector2(-3f, 1f), 0.025f).SetEase(Ease.Linear))
                .Append(_impactRoot.DOAnchorPos(_impactBasePosition + new Vector2(2f, -1f), 0.030f).SetEase(Ease.Linear))
                .Append(_impactRoot.DOAnchorPos(_impactBasePosition, 0.035f).SetEase(Ease.Linear));
            var scaleSequence = DOTween.Sequence()
                .Append(_impactRoot.DOScale(_impactBaseScale * 1.022f, 0.025f).SetEase(Ease.Linear))
                .AppendInterval(0.055f)
                .Append(_impactRoot.DOScale(_impactBaseScale, 0.035f).SetEase(Ease.Linear));

            _impactTween = DOTween.Sequence()
                .Append(positionSequence)
                .Join(scaleSequence)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    RestoreImpactTransform();
                    _impactTween = null;
                })
                .OnKill(() => _impactTween = null);

            _acceptedBurst.Emit(AcceptedBurstCount);
        }

        public void RestoreImmediate()
        {
            KillIdleTween();
            KillShineTween(reset: true);
            KillImpactTween(restore: true);
            StopAcceptedBurst();
            RestoreGlow();
            ResetShine();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            InitializeOrThrow();
            RestoreGlow();
            ResetShine();
            RestoreImpactTransform();
            StartIdleTween();
        }

        private void OnDisable()
        {
            RestoreImmediate();
            _lastShineCommand = MainMenuCommandId.None;
            _lastShineTime = double.NegativeInfinity;
        }

        private void OnDestroy()
        {
            RestoreImmediate();
            if (_logoImage != null)
            {
                _logoImage.material = _logoOriginalMaterial;
            }

            if (_logoRuntimeMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_logoRuntimeMaterial);
                }
                else
                {
                    DestroyImmediate(_logoRuntimeMaterial);
                }

                _logoRuntimeMaterial = null;
            }
        }

        private void InitializeOrThrow()
        {
            if (_initialized)
            {
                return;
            }

            ValidateAuthoredStructureOrThrow();
            _impactBasePosition = _impactRoot.anchoredPosition;
            _impactBaseScale = _impactRoot.localScale;
            _glowBaseScale = _glowRoot.localScale;
            _logoOriginalMaterial = _logoImage.material;
            _logoRuntimeMaterial = new Material(_allIn1Template)
            {
                name = $"{_logoImage.name}_AllIn1Shine_Runtime",
            };
            _logoRuntimeMaterial.SetFloat(ShineLocationId, ShineStartLocation);
            _logoRuntimeMaterial.SetFloat(ShineGlowId, 0f);
            _logoImage.material = _logoRuntimeMaterial;
            _initialized = true;
        }

        private void StartIdleTween()
        {
            KillIdleTween();
            _glowRoot.localScale = _glowBaseScale;
            _glowCanvasGroup.alpha = IdleMinimumAlpha;
            _idleTween = DOTween.Sequence()
                .Append(_glowRoot.DOScale(_glowBaseScale * IdleScaleMultiplier, IdleHalfCycleSeconds))
                .Join(_glowCanvasGroup.DOFade(IdleMaximumAlpha, IdleHalfCycleSeconds))
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnKill(() => _idleTween = null);
        }

        private bool CanPlay()
        {
            return isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        private void RestoreGlow()
        {
            if (!_initialized)
            {
                return;
            }

            _glowRoot.localScale = _glowBaseScale;
            _glowCanvasGroup.alpha = IdleMinimumAlpha;
        }

        private void RestoreImpactTransform()
        {
            if (!_initialized)
            {
                return;
            }

            _impactRoot.anchoredPosition = _impactBasePosition;
            _impactRoot.localScale = _impactBaseScale;
        }

        private void ResetShine()
        {
            if (_logoRuntimeMaterial == null)
            {
                return;
            }

            _logoRuntimeMaterial.SetFloat(ShineLocationId, ShineStartLocation);
            _logoRuntimeMaterial.SetFloat(ShineGlowId, 0f);
        }

        private void StopAcceptedBurst()
        {
            if (_acceptedBurst != null)
            {
                _acceptedBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void KillIdleTween()
        {
            if (_idleTween == null)
            {
                return;
            }

            _idleTween.Kill(false);
            _idleTween = null;
        }

        private void KillShineTween(bool reset)
        {
            if (_shineTween != null)
            {
                _shineTween.Kill(false);
                _shineTween = null;
            }

            if (reset)
            {
                ResetShine();
            }
        }

        private void KillImpactTween(bool restore)
        {
            if (_impactTween != null)
            {
                _impactTween.Kill(false);
                _impactTween = null;
            }

            if (restore)
            {
                RestoreImpactTransform();
            }
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(MainMenuLogoEffectView)} is missing authored reference '{fieldName}'. Repair MainMenuScreen.prefab.");
            }
        }
    }
}
