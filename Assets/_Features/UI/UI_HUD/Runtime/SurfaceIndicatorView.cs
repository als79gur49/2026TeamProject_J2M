using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace Game.Feature.UI.HUD
{
    public sealed class SurfaceIndicatorView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private SurfaceCubeMapView _cubeMapView;
        [FormerlySerializedAs("_transitionLabel")]
        [SerializeField] private TMP_Text _currentSurfaceText;
        [SerializeField] private Image _progressBar;
        [FormerlySerializedAs("_faceChips")]
        [SerializeField] private FaceChipView[] _legacyFaceChips;

        private SurfaceIndicatorViewModel _viewModel;

        public SurfaceIndicatorViewModel ViewModel => _viewModel;

        public SurfaceCubeMapView CubeMapView => _cubeMapView;

        public void Bind(SurfaceIndicatorViewModel viewModel)
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

            if (_cubeMapView != null)
            {
                _cubeMapView.Bind(_viewModel);
            }

            RefreshView();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_root, nameof(_root));
            RequireReference(_cubeMapView, nameof(_cubeMapView));
            RequireReference(_currentSurfaceText, nameof(_currentSurfaceText));
            _cubeMapView.ValidateAuthoredStructureOrThrow();
        }

        private void OnEnable()
        {
            RefreshView();
        }

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            if (_cubeMapView != null)
            {
                _cubeMapView.Bind(null);
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

            HideLegacyFaceChips();

            if (_viewModel == null)
            {
                return;
            }

            if (_currentSurfaceText != null)
            {
                _currentSurfaceText.text = _viewModel.IsTransitionActive
                    ? _viewModel.SurfaceStateText
                    : _viewModel.CurrentFaceLabel;
            }

            if (_progressBar != null)
            {
                _progressBar.gameObject.SetActive(_viewModel.IsTransitionActive);
                _progressBar.fillAmount = _viewModel.Progress01;
            }
        }

        private void HideLegacyFaceChips()
        {
            if (_legacyFaceChips == null)
            {
                return;
            }

            for (var i = 0; i < _legacyFaceChips.Length; i++)
            {
                if (_legacyFaceChips[i] != null)
                {
                    _legacyFaceChips[i].gameObject.SetActive(false);
                }
            }
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceIndicatorView)} is missing authored reference '{fieldName}'.");
            }
        }

    }
}
