using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class TopologyTransitionPostFxController : MonoBehaviour
    {
        private const string RuntimeVolumeObjectName = "TopologyTransitionRuntimeVolume";

        private LensDistortion _lensDistortion;
        private MotionBlur _motionBlur;
        private Camera _outputCamera;
        private UniversalAdditionalCameraData _outputCameraData;
        private TopologyTransitionPostFxProfile _profile = TopologyTransitionPostFxProfile.CreateDefault();
        private Volume _runtimeVolume;
        private VolumeProfile _runtimeVolumeProfile;

        public LensDistortion LensDistortionOverride => _lensDistortion;

        public MotionBlur MotionBlurOverride => _motionBlur;

        public Camera OutputCamera => _outputCamera;

        public Volume RuntimeVolume => _runtimeVolume;

        public VolumeProfile RuntimeVolumeProfile => _runtimeVolumeProfile;

        public void Initialize(TopologyTransitionPostFxProfile profile, Camera outputCamera)
        {
            _profile = profile?.Clone() ?? TopologyTransitionPostFxProfile.CreateDefault();
            _outputCamera = outputCamera;
            ConfigureOutputCamera(_outputCamera);
            RebuildRuntimeVolume();
            ResetIntensity();
        }

        public void Apply(in TopologyTransitionVisualState visualState)
        {
            if (_motionBlur == null &&
                _lensDistortion == null)
            {
                return;
            }

            var hasChanged = false;

            if (_motionBlur != null)
            {
                var blurIntensity = _profile.EvaluateMotionBlurIntensity(visualState);
                if (!Mathf.Approximately(_motionBlur.intensity.value, blurIntensity))
                {
                    _motionBlur.intensity.value = blurIntensity;
                    hasChanged = true;
                }
            }

            if (_lensDistortion != null)
            {
                var distortionIntensity = _profile.EvaluateDistortionIntensity(visualState);
                if (!Mathf.Approximately(_lensDistortion.intensity.value, distortionIntensity))
                {
                    _lensDistortion.intensity.value = distortionIntensity;
                    hasChanged = true;
                }
            }

            if (hasChanged)
            {
                NotifyRuntimeProfileChanged();
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeProfileInstance();
        }

        private void ConfigureOutputCamera(Camera outputCamera)
        {
            if (outputCamera == null)
            {
                _outputCameraData = null;
                return;
            }

            _outputCameraData = outputCamera.GetUniversalAdditionalCameraData();
            _outputCameraData.renderPostProcessing = true;
        }

        private void RebuildRuntimeVolume()
        {
            var volumeRoot = ResolveOrCreateRuntimeVolumeRoot();
            _runtimeVolume = volumeRoot.GetComponent<Volume>() ?? volumeRoot.AddComponent<Volume>();
            _runtimeVolume.isGlobal = true;
            _runtimeVolume.priority = 100f;
            _runtimeVolume.blendDistance = 0f;
            _runtimeVolume.weight = 1f;

            DestroyRuntimeProfileInstance();

            var sourceProfile = _profile.AuthoritativeVolumeProfile;
            if (sourceProfile == null)
            {
                _runtimeVolume.enabled = false;
                _runtimeVolume.sharedProfile = null;
                _runtimeVolume.profile = null;
                _lensDistortion = null;
                _motionBlur = null;
                return;
            }

            _runtimeVolumeProfile = CreateRuntimeProfileClone(sourceProfile);
            _runtimeVolume.sharedProfile = sourceProfile;
            _runtimeVolume.profile = _runtimeVolumeProfile;
            _runtimeVolume.enabled = true;

            if (!_runtimeVolumeProfile.TryGet(out _motionBlur) || _motionBlur == null)
            {
                _motionBlur = _runtimeVolumeProfile.Add<MotionBlur>(overrides: true);
            }

            if (!_runtimeVolumeProfile.TryGet(out _lensDistortion) || _lensDistortion == null)
            {
                _lensDistortion = _runtimeVolumeProfile.Add<LensDistortion>(overrides: true);
            }

            _profile.ApplyMotionBlurDefaults(_motionBlur);
            _profile.ApplyDistortionDefaults(_lensDistortion);
            NotifyRuntimeProfileChanged();
        }

        // VolumeProfile.Instantiate does not deep-clone its component list, so clone components explicitly.
        private static VolumeProfile CreateRuntimeProfileClone(VolumeProfile sourceProfile)
        {
            var runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = $"{sourceProfile.name} (TopologyTransitionRuntime)";

            foreach (var component in sourceProfile.components)
            {
                runtimeProfile.components.Add(Instantiate(component));
            }

            return runtimeProfile;
        }

        private GameObject ResolveOrCreateRuntimeVolumeRoot()
        {
            var volumeTransform = transform.Find(RuntimeVolumeObjectName);
            if (volumeTransform != null)
            {
                return volumeTransform.gameObject;
            }

            var volumeObject = new GameObject(RuntimeVolumeObjectName);
            volumeObject.transform.SetParent(transform, worldPositionStays: false);
            return volumeObject;
        }

        private void ResetIntensity()
        {
            var hasChanged = false;

            if (_motionBlur != null)
            {
                _motionBlur.intensity.value = 0f;
                hasChanged = true;
            }

            if (_lensDistortion != null)
            {
                _lensDistortion.intensity.value = 0f;
                hasChanged = true;
            }

            if (hasChanged)
            {
                NotifyRuntimeProfileChanged();
            }
        }

        private void DestroyRuntimeProfileInstance()
        {
            if (_runtimeVolume != null)
            {
                _runtimeVolume.profile = null;
                _runtimeVolume.sharedProfile = null;
            }

            if (_runtimeVolumeProfile == null)
            {
                _lensDistortion = null;
                _motionBlur = null;
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeVolumeProfile);
            }
            else
            {
                DestroyImmediate(_runtimeVolumeProfile);
            }

            _runtimeVolumeProfile = null;
            _lensDistortion = null;
            _motionBlur = null;
        }

        private void NotifyRuntimeProfileChanged()
        {
            _runtimeVolumeProfile?.Reset();
        }
    }
}
