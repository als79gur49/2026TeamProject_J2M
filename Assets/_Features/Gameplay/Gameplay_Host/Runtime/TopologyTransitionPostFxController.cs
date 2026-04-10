using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class TopologyTransitionPostFxController : MonoBehaviour
    {
        private const string RuntimeVolumeObjectName = "TopologyTransitionRuntimeVolume";

        private MotionBlur _motionBlur;
        private Camera _outputCamera;
        private UniversalAdditionalCameraData _outputCameraData;
        private TopologyTransitionPostFxProfile _profile = TopologyTransitionPostFxProfile.CreateDefault();
        private Volume _runtimeVolume;
        private VolumeProfile _runtimeVolumeProfile;

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
            if (_motionBlur == null)
            {
                return;
            }

            var intensity = _profile.EvaluateIntensity(visualState);
            if (Mathf.Approximately(_motionBlur.intensity.value, intensity))
            {
                return;
            }

            _motionBlur.intensity.value = intensity;
            NotifyRuntimeProfileChanged();
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
                _motionBlur = null;
                return;
            }

            _runtimeVolumeProfile = Object.Instantiate(sourceProfile);
            _runtimeVolumeProfile.name = $"{sourceProfile.name} (TopologyTransitionRuntime)";
            _runtimeVolume.sharedProfile = sourceProfile;
            _runtimeVolume.profile = _runtimeVolumeProfile;
            _runtimeVolume.enabled = true;

            if (!_runtimeVolumeProfile.TryGet(out _motionBlur) || _motionBlur == null)
            {
                _motionBlur = _runtimeVolumeProfile.Add<MotionBlur>(overrides: true);
            }

            _profile.ApplyDefaults(_motionBlur);
            NotifyRuntimeProfileChanged();
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
            if (_motionBlur != null)
            {
                _motionBlur.intensity.value = 0f;
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
            _motionBlur = null;
        }

        private void NotifyRuntimeProfileChanged()
        {
            _runtimeVolumeProfile?.Reset();
        }
    }
}
