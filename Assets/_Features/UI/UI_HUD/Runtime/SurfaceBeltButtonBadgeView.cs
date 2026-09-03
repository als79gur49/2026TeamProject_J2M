using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class SurfaceBeltButtonBadgeView : MonoBehaviour
    {
        internal const string AllIn1UiMaskShaderName = "AllIn1SpriteShader/AllIn1SpriteShaderUiMask";

        private const string ShineKeyword = "SHINE_ON";
        private static readonly int ShineColorId = Shader.PropertyToID("_ShineColor");
        private static readonly int ShineLocationId = Shader.PropertyToID("_ShineLocation");
        private static readonly int ShineRotateId = Shader.PropertyToID("_ShineRotate");
        private static readonly int ShineWidthId = Shader.PropertyToID("_ShineWidth");
        private static readonly int ShineGlowId = Shader.PropertyToID("_ShineGlow");

        [SerializeField] private Image _frame;
        [SerializeField] private Image _background;
        [SerializeField] private RectTransform _motionRoot;
        [SerializeField] private Material _shineMaterialTemplate;

        private Sequence _transitionSequence;
        private Material _backgroundOriginalMaterial;
        private Material _shineMaterialInstance;
        private Vector3 _authoredMotionScale = Vector3.one;
        private ButtonBadgeVisualStyle _lastVisualStyle;
        private ButtonBadgeTransitionStyle _lastTransitionStyle;
        private int _lastSlotIndex;
        private bool _hasAuthoredMotionScale;
        private bool _hasBoundState;
        private bool _lastActive;

        public void Bind(
            int slotIndex,
            bool isActive,
            ButtonBadgeVisualStyle visualStyle,
            ButtonBadgeTransitionStyle transitionStyle)
        {
            ValidateAuthoredStructureOrThrow();
            CacheAuthoredMotionScale();
            gameObject.SetActive(true);

            var normalizedSlotIndex = SurfaceBeltSlotMapping.WrapSlot(slotIndex);
            if (!_hasBoundState)
            {
                StoreBoundState(normalizedSlotIndex, isActive, visualStyle, transitionStyle);
                ApplyStableVisuals();
                return;
            }

            var slotChanged = normalizedSlotIndex != _lastSlotIndex;
            var activeChanged = isActive != _lastActive;
            StoreBoundState(normalizedSlotIndex, isActive, visualStyle, transitionStyle);

            if (!slotChanged && !activeChanged)
            {
                return;
            }

            if (isActive && activeChanged)
            {
                PlayActivation();
                return;
            }

            if (!isActive && activeChanged)
            {
                PlayDeactivation();
                return;
            }

            if (isActive)
            {
                PlaySlotConfirmation();
                return;
            }

            ApplyStableVisuals();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_frame, nameof(_frame));
            RequireReference(_background, nameof(_background));
            RequireReference(_motionRoot, nameof(_motionRoot));
            RequireReference(_shineMaterialTemplate, nameof(_shineMaterialTemplate));
            if (_shineMaterialTemplate.shader == null ||
                !string.Equals(_shineMaterialTemplate.shader.name, AllIn1UiMaskShaderName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{nameof(SurfaceBeltButtonBadgeView)} requires shader '{AllIn1UiMaskShaderName}'.");
            }
        }

        private void OnDisable()
        {
            KillTransition();
            if (_hasBoundState)
            {
                ApplyStableVisuals();
            }
        }

        private void OnDestroy()
        {
            KillTransition();
            DisposeShineMaterialInstance();
        }

        private void PlayActivation()
        {
            KillTransition();
            var duration = Mathf.Max(0.01f, _lastTransitionStyle.ActivationDurationSeconds);
            var riseDuration = duration * 0.55f;
            var settleDuration = duration - riseDuration;
            var activeColor = _lastVisualStyle.Active.BackgroundColor;

            _transitionSequence = DOTween.Sequence()
                .SetUpdate(_lastTransitionStyle.UseUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            _transitionSequence
                .Append(_motionRoot
                    .DOScale(ScaleFromAuthored(_lastTransitionStyle.ActivationPeakScale), riseDuration)
                    .SetEase(_lastTransitionStyle.ActivationEase))
                .Append(_motionRoot
                    .DOScale(ScaleFromAuthored(1.0f), settleDuration)
                    .SetEase(_lastTransitionStyle.SettleEase))
                .Insert(0.0f, _frame.DOColor(activeColor, duration).SetEase(_lastTransitionStyle.SettleEase))
                .Insert(0.0f, _background.DOColor(activeColor, duration).SetEase(_lastTransitionStyle.SettleEase));
            InsertShine(_transitionSequence);
            RegisterTransitionCompletion();
        }

        private void PlayDeactivation()
        {
            KillTransition();
            var duration = Mathf.Max(0.01f, _lastTransitionStyle.DeactivationDurationSeconds);
            var inactiveColor = _lastVisualStyle.Inactive.BackgroundColor;

            _transitionSequence = DOTween.Sequence()
                .SetUpdate(_lastTransitionStyle.UseUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .Append(_motionRoot
                    .DOScale(ScaleFromAuthored(_lastTransitionStyle.InactiveScale), duration)
                    .SetEase(_lastTransitionStyle.SettleEase))
                .Join(_frame.DOColor(inactiveColor, duration).SetEase(_lastTransitionStyle.SettleEase))
                .Join(_background.DOColor(inactiveColor, duration).SetEase(_lastTransitionStyle.SettleEase));
            RegisterTransitionCompletion();
        }

        private void PlaySlotConfirmation()
        {
            KillTransition();
            var duration = Mathf.Max(0.01f, _lastTransitionStyle.SlotConfirmationDurationSeconds);
            var riseDuration = duration * 0.45f;
            var settleDuration = duration - riseDuration;

            _transitionSequence = DOTween.Sequence()
                .SetUpdate(_lastTransitionStyle.UseUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .Append(_motionRoot
                    .DOScale(ScaleFromAuthored(_lastTransitionStyle.SlotConfirmationPeakScale), riseDuration)
                    .SetEase(_lastTransitionStyle.SettleEase))
                .Append(_motionRoot
                    .DOScale(ScaleFromAuthored(1.0f), settleDuration)
                    .SetEase(_lastTransitionStyle.SettleEase));
            InsertShine(_transitionSequence);
            RegisterTransitionCompletion();
        }

        private void InsertShine(Sequence sequence)
        {
            EnsureShineMaterialInstance();
            _shineMaterialInstance.SetColor(ShineColorId, _lastTransitionStyle.ShineColor);
            _shineMaterialInstance.SetFloat(ShineRotateId, _lastTransitionStyle.ShineRotateRadians);
            _shineMaterialInstance.SetFloat(ShineWidthId, _lastTransitionStyle.ShineWidth);
            _shineMaterialInstance.SetFloat(ShineGlowId, _lastTransitionStyle.ShineGlow);
            _shineMaterialInstance.SetFloat(ShineLocationId, 0.0f);

            sequence.Insert(
                0.0f,
                DOTween.To(
                        () => _shineMaterialInstance != null
                            ? _shineMaterialInstance.GetFloat(ShineLocationId)
                            : 0.0f,
                        value =>
                        {
                            if (_shineMaterialInstance != null)
                            {
                                _shineMaterialInstance.SetFloat(ShineLocationId, value);
                            }
                        },
                        1.0f,
                        Mathf.Max(0.01f, _lastTransitionStyle.ShineDurationSeconds))
                    .SetEase(_lastTransitionStyle.SettleEase));
        }

        private void RegisterTransitionCompletion()
        {
            _transitionSequence
                .OnComplete(() =>
                {
                    _transitionSequence = null;
                    ResetShine();
                    ApplyStableVisuals();
                })
                .OnKill(() =>
                {
                    _transitionSequence = null;
                    ResetShine();
                });
        }

        private void ApplyStableVisuals()
        {
            CacheAuthoredMotionScale();
            var stateStyle = _lastVisualStyle.GetStateStyle(_lastActive);
            _frame.color = stateStyle.BackgroundColor;
            _background.color = stateStyle.BackgroundColor;
            _motionRoot.localScale = ScaleFromAuthored(
                _lastActive ? 1.0f : _lastTransitionStyle.InactiveScale);
            ResetShine();
        }

        private void StoreBoundState(
            int slotIndex,
            bool isActive,
            ButtonBadgeVisualStyle visualStyle,
            ButtonBadgeTransitionStyle transitionStyle)
        {
            _lastSlotIndex = slotIndex;
            _lastActive = isActive;
            _lastVisualStyle = visualStyle;
            _lastTransitionStyle = transitionStyle;
            _hasBoundState = true;
        }

        private void CacheAuthoredMotionScale()
        {
            if (_hasAuthoredMotionScale || _motionRoot == null)
            {
                return;
            }

            _authoredMotionScale = _motionRoot.localScale;
            _hasAuthoredMotionScale = true;
        }

        private Vector3 ScaleFromAuthored(float multiplier)
        {
            return _authoredMotionScale * Mathf.Max(0.0f, multiplier);
        }

        private void KillTransition()
        {
            if (_transitionSequence != null)
            {
                _transitionSequence.Kill(false);
                _transitionSequence = null;
            }

            ResetShine();
        }

        private void EnsureShineMaterialInstance()
        {
            if (_shineMaterialInstance != null)
            {
                return;
            }

            _backgroundOriginalMaterial = _background.material;
            _shineMaterialInstance = new Material(_shineMaterialTemplate)
            {
                name = $"{name}_AllIn1Shine_Runtime",
            };
            _shineMaterialInstance.EnableKeyword(ShineKeyword);
            _shineMaterialInstance.SetFloat(ShineGlowId, 0.0f);
            _background.material = _shineMaterialInstance;
        }

        private void ResetShine()
        {
            if (_shineMaterialInstance != null)
            {
                _shineMaterialInstance.SetFloat(ShineGlowId, 0.0f);
            }
        }

        private void DisposeShineMaterialInstance()
        {
            if (_background != null && _background.material == _shineMaterialInstance)
            {
                _background.material = _backgroundOriginalMaterial;
            }

            if (_shineMaterialInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_shineMaterialInstance);
            }
            else
            {
                DestroyImmediate(_shineMaterialInstance);
            }

            _shineMaterialInstance = null;
            _backgroundOriginalMaterial = null;
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceBeltButtonBadgeView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
