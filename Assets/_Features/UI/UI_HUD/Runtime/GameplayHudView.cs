using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class GameplayHudView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _hpLabel;
        [SerializeField] private Text _facingLabel;
        [SerializeField] private Text _actionLabel;
        [SerializeField] private Text _topologyLabel;
        [SerializeField] private Text _pausedLabel;
        [SerializeField] private Text _readyLabel;
        [SerializeField] private Text _feedbackLabel;
        [SerializeField] private Button _moveUpButton;
        [SerializeField] private Button _flipRightButton;
        [SerializeField] private Button _pauseButton;

        public event Action MoveUpRequested;

        public event Action FlipRightRequested;

        public event Action PauseRequested;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public GameplayHudViewModel ViewModel { get; private set; }

        private bool _isVisible = true;

        public void Configure(
            GameObject root,
            Text titleLabel,
            Text hpLabel,
            Text facingLabel,
            Text actionLabel,
            Text topologyLabel,
            Text pausedLabel,
            Text readyLabel,
            Text feedbackLabel,
            Button moveUpButton,
            Button flipRightButton,
            Button pauseButton)
        {
            _root = root;
            _titleLabel = titleLabel;
            _hpLabel = hpLabel;
            _facingLabel = facingLabel;
            _actionLabel = actionLabel;
            _topologyLabel = topologyLabel;
            _pausedLabel = pausedLabel;
            _readyLabel = readyLabel;
            _feedbackLabel = feedbackLabel;
            _moveUpButton = moveUpButton;
            _flipRightButton = flipRightButton;
            _pauseButton = pauseButton;

            _moveUpButton.onClick.RemoveListener(ClickMoveUp);
            _flipRightButton.onClick.RemoveListener(ClickFlipRight);
            _pauseButton.onClick.RemoveListener(ClickPause);
            _moveUpButton.onClick.AddListener(ClickMoveUp);
            _flipRightButton.onClick.AddListener(ClickFlipRight);
            _pauseButton.onClick.AddListener(ClickPause);

            RefreshView();
        }

        public void Bind(GameplayHudViewModel viewModel)
        {
            if (ViewModel != null)
            {
                ViewModel.Changed -= HandleViewModelChanged;
            }

            ViewModel = viewModel;
            if (ViewModel != null)
            {
                ViewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

        public void ClickFlipRight()
        {
            if (!IsVisible || ViewModel == null || !ViewModel.IsInteractive)
            {
                return;
            }

            FlipRightRequested?.Invoke();
        }

        public void ClickMoveUp()
        {
            if (!IsVisible || ViewModel == null || !ViewModel.IsInteractive)
            {
                return;
            }

            MoveUpRequested?.Invoke();
        }

        public void ClickPause()
        {
            if (!IsVisible || ViewModel == null || !ViewModel.IsInteractive)
            {
                return;
            }

            PauseRequested?.Invoke();
        }

        private void OnDestroy()
        {
            if (ViewModel != null)
            {
                ViewModel.Changed -= HandleViewModelChanged;
            }
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            if (ViewModel == null)
            {
                if (_feedbackLabel != null)
                {
                    _feedbackLabel.text = string.Empty;
                }

                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = "Gameplay HUD";
            }

            if (_hpLabel != null)
            {
                _hpLabel.text = $"HP: {ViewModel.CurrentHp}";
            }

            if (_facingLabel != null)
            {
                _facingLabel.text = $"Facing: {ViewModel.FacingText}";
            }

            if (_actionLabel != null)
            {
                _actionLabel.text = $"Action: {ViewModel.ActiveActionText}";
            }

            if (_topologyLabel != null)
            {
                _topologyLabel.text = $"Topology: {ViewModel.TopologyText}";
            }

            if (_pausedLabel != null)
            {
                _pausedLabel.text = $"Paused: {ViewModel.IsPaused}";
            }

            if (_readyLabel != null)
            {
                _readyLabel.text = $"Ready: {ViewModel.CanAcceptGameplayCommands}";
            }

            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = string.IsNullOrEmpty(ViewModel.FeedbackText)
                    ? string.Empty
                    : $"Feedback: {ViewModel.FeedbackText}";
            }

            if (_moveUpButton != null)
            {
                _moveUpButton.interactable = ViewModel.IsInteractive;
            }

            if (_flipRightButton != null)
            {
                _flipRightButton.interactable = ViewModel.IsInteractive;
            }

            if (_pauseButton != null)
            {
                _pauseButton.interactable = ViewModel.IsInteractive;
            }
        }
    }
}
