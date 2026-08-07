using System.Collections.Generic;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal sealed class GenericLoadingOverlayContentView : SceneTransitionOverlayContentView
    {
        [SerializeField] private TMP_Text _loadingLabel;
        [SerializeField] private GameplayUiTypographyTheme _typographyTheme;

        private bool _hasCapturedLoadingTypography;
        private TmpTypographyAuthoredState _loadingAuthoredState;

        public override void Bind(SceneTransitionOverlayModel model)
        {
            base.Bind(model);
            CaptureLoadingTypography();
            SetText(_loadingLabel, model.Text.LoadingLabel);

            var localeCode = string.IsNullOrWhiteSpace(model.Text.LocaleCode)
                ? UnityStringTableTextResolver.DefaultLocaleCode
                : model.Text.LocaleCode;
            GameplayHudLocalizationBinding.ApplyTypography(
                _loadingLabel,
                _typographyTheme,
                localeCode,
                TypographyStyleTag.Label,
                _loadingAuthoredState);
        }

        internal override IReadOnlyList<string> CollectValidationIssues()
        {
            var issues = new List<string>(base.CollectValidationIssues());
            AddMissing(issues, _loadingLabel, nameof(_loadingLabel));
            AddMissing(issues, _typographyTheme, nameof(_typographyTheme));
            return issues;
        }

        private void CaptureLoadingTypography()
        {
            if (_hasCapturedLoadingTypography)
            {
                return;
            }

            _loadingAuthoredState = TmpTypographyAuthoredState.Capture(_loadingLabel);
            _hasCapturedLoadingTypography = true;
        }
    }
}
