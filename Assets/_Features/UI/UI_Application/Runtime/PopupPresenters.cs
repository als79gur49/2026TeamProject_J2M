using System;
using System.Collections.Generic;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Application
{
    public sealed class PausePopupPresenter
    {
        public PausePopupPresenter()
        {
            ViewModel = new PausePopupViewModel();
        }

        public PausePopupViewModel ViewModel { get; }

        public void Apply(PausePopupPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.DescriptionText,
                payload.ResumeLabel,
                payload.ObjectiveLabel,
                payload.SettingsLabel);
        }
    }

    public sealed class ObjectiveInfoPopupPresenter
    {
        public ObjectiveInfoPopupPresenter()
        {
            ViewModel = new ObjectiveInfoPopupViewModel();
        }

        public ObjectiveInfoPopupViewModel ViewModel { get; }

        public void Apply(ObjectiveInfoPopupPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(payload.TitleText, payload.BodyText, payload.CloseLabel);
        }
    }

    public sealed class ConfirmPopupPresenter
    {
        public ConfirmPopupPresenter()
        {
            ViewModel = new ConfirmPopupViewModel();
        }

        public ConfirmPopupViewModel ViewModel { get; }

        public void Apply(ConfirmPopupPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.BodyText,
                payload.ConfirmLabel,
                payload.CancelLabel,
                payload.IsConfirmDestructive);
        }
    }

    public sealed class TooltipPopupPresenter
    {
        public TooltipPopupPresenter()
        {
            ViewModel = new TooltipPopupViewModel();
        }

        public TooltipPopupViewModel ViewModel { get; }

        public void Apply(TooltipPopupPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(payload.TitleText, payload.BodyText, payload.AnchorPreset);
        }
    }

    public sealed class RewardPopupPresenter
    {
        public RewardPopupPresenter()
        {
            ViewModel = new RewardPopupViewModel();
        }

        public RewardPopupViewModel ViewModel { get; }

        public void Apply(RewardPopupPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var itemLines = new List<string>(payload.Items.Count);
            for (var i = 0; i < payload.Items.Count; i++)
            {
                var item = payload.Items[i];
                itemLines.Add($"{item.LabelText} x{item.Amount}");
            }

            ViewModel.SetContent(
                payload.TitleText,
                itemLines,
                payload.SummaryText,
                payload.CloseLabel);
        }
    }
}
