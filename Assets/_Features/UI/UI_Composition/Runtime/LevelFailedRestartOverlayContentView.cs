using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal sealed class LevelFailedRestartOverlayContentView : SceneTransitionOverlayContentView
    {
        [SerializeField] private TMP_Text _levelRestartMessageText;

        public override void Bind(SceneTransitionOverlayModel model)
        {
            base.Bind(model);
            SetText(_levelRestartMessageText, "Returning to the first stage in this level.");
        }

        public override void ResetView()
        {
            base.ResetView();
            SetText(_levelRestartMessageText, string.Empty);
        }

        internal override IReadOnlyList<string> CollectValidationIssues()
        {
            var issues = new List<string>(base.CollectValidationIssues());
            AddMissing(issues, _levelRestartMessageText, nameof(_levelRestartMessageText));
            return issues;
        }
    }
}
